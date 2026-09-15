using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Common;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Persistence.Search;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class SearchRepository(
    ApplicationDbContext dbContext,
    IOptions<TopRatedOptions> topRatedOptions) : ISearchRepository
{
    public async Task<PaginatedResult<SearchItem>> SearchAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(criteria.Query)
            ? null
            : QueryNormalizer.Normalize(criteria.Query);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(dbContext, criteria, normalizedQuery);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplySort(combinedQuery, criteria.Sort, normalizedQuery)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = QueryNormalizer.Normalize(query);
        var searchCriteria = new SearchCriteria(
            query,
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            limit);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(dbContext, searchCriteria, normalizedQuery);

        var projections = await SearchQueryBuilder
            .ApplyRelevanceSort(combinedQuery, normalizedQuery)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return projections
            .Select(item => new SearchSuggestion(item.Id, item.Type, item.Title, item.PosterUrl))
            .ToList();
    }

    public async Task<PaginatedResult<SearchItem>> GetPopularAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var searchCriteria = new SearchCriteria(
            null,
            criteria.Type,
            null,
            null,
            null,
            null,
            SearchSortOption.Popular,
            criteria.Page,
            criteria.PageSize);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(dbContext, searchCriteria, null);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplySort(combinedQuery, SearchSortOption.Popular, null)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<PaginatedResult<SearchItem>> GetTrendingAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var searchCriteria = new SearchCriteria(
            null,
            criteria.Type,
            null,
            null,
            null,
            null,
            SearchSortOption.Popular,
            criteria.Page,
            criteria.PageSize);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(dbContext, searchCriteria, null);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplyTrendingSort(combinedQuery)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var combinedQuery = SearchQueryBuilder.BuildNewReleasesQuery(dbContext, criteria);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplyNewReleasesSort(combinedQuery)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    public async Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var combinedQuery = SearchQueryBuilder.BuildTopRatedQuery(dbContext, criteria);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);
        var catalogMean = await GetCatalogMeanVoteAverageAsync(criteria.Type, cancellationToken);
        var minimumVoteConfidence = topRatedOptions.Value.MinimumVoteConfidence;
        var skip = (criteria.Page - 1) * criteria.PageSize;

        List<SearchItemProjection> items;

        if (UsesDatabaseTopRatedRanking())
        {
            items = await SearchQueryBuilder
                .ApplyTopRatedSort(combinedQuery, catalogMean, minimumVoteConfidence)
                .Skip(skip)
                .Take(criteria.PageSize)
                .ToListAsync(cancellationToken);
        }
        else
        {
            var rankedItems = await combinedQuery.ToListAsync(cancellationToken);
            items = rankedItems
                .OrderByDescending(item => TopRatedScoreCalculator.ComputeWeightedRating(
                    item.VoteAverage,
                    item.VoteCount,
                    catalogMean,
                    minimumVoteConfidence))
                .ThenByDescending(item => item.VoteCount)
                .ThenBy(item => item.Title)
                .ThenBy(item => item.Type)
                .ThenBy(item => item.Id)
                .Skip(skip)
                .Take(criteria.PageSize)
                .ToList();
        }

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    private bool UsesDatabaseTopRatedRanking() =>
        dbContext.Database.IsRelational() &&
        !string.Equals(
            dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

    public async Task<decimal> GetCatalogMeanVoteAverageAsync(
        SearchContentType type,
        CancellationToken cancellationToken = default)
    {
        var movieAverages = dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.VoteCount > 0)
            .Select(movie => movie.VoteAverage);

        var tvAverages = dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.VoteCount > 0)
            .Select(tvShow => tvShow.VoteAverage);

        IQueryable<decimal> averages = type switch
        {
            SearchContentType.Movie => movieAverages,
            SearchContentType.Tv => tvAverages,
            _ => movieAverages.Concat(tvAverages)
        };

        if (!await averages.AnyAsync(cancellationToken))
        {
            return 6.0m;
        }

        return await averages.AverageAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(
        IReadOnlyList<SearchItem> items,
        Guid genreId,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return new HashSet<CatalogContentKey>();
        }

        var keys = new HashSet<CatalogContentKey>();
        var movieIds = items
            .Where(item => item.Type == "movie")
            .Select(item => item.Id)
            .Distinct()
            .ToList();
        var tvShowIds = items
            .Where(item => item.Type == "tv")
            .Select(item => item.Id)
            .Distinct()
            .ToList();

        if (movieIds.Count > 0)
        {
            var matchingMovieIds = await dbContext.MovieGenres
                .AsNoTracking()
                .Where(movieGenre => movieGenre.GenreId == genreId && movieIds.Contains(movieGenre.MovieId))
                .Select(movieGenre => movieGenre.MovieId)
                .ToListAsync(cancellationToken);

            foreach (var movieId in matchingMovieIds)
            {
                keys.Add(new CatalogContentKey(movieId, "movie"));
            }
        }

        if (tvShowIds.Count > 0)
        {
            var matchingTvShowIds = await dbContext.TvShowGenres
                .AsNoTracking()
                .Where(tvShowGenre => tvShowGenre.GenreId == genreId && tvShowIds.Contains(tvShowGenre.TvShowId))
                .Select(tvShowGenre => tvShowGenre.TvShowId)
                .ToListAsync(cancellationToken);

            foreach (var tvShowId in matchingTvShowIds)
            {
                keys.Add(new CatalogContentKey(tvShowId, "tv"));
            }
        }

        return keys;
    }

    public async Task<PaginatedResult<SearchItem>> GetByGenreAsync(
        string genreName,
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var searchCriteria = new SearchCriteria(
            null,
            criteria.Type,
            null,
            null,
            null,
            null,
            SearchSortOption.Popular,
            criteria.Page,
            criteria.PageSize);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(dbContext, searchCriteria, null, genreName);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplySort(combinedQuery, SearchSortOption.Popular, null)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    private static PaginatedResult<SearchItem> ToPaginatedResult(
        IReadOnlyList<SearchItemProjection> items,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<SearchItem>(
            items.Select(ToSearchItem).ToList(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    private static SearchItem ToSearchItem(SearchItemProjection projection) =>
        new(
            projection.Id,
            projection.Type,
            projection.Title,
            projection.OriginalTitle,
            projection.Overview,
            projection.PosterUrl,
            projection.BackdropUrl,
            projection.ReleaseDate,
            projection.VoteAverage,
            projection.VoteCount,
            projection.Year);
}
