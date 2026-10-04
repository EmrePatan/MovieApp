using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Common;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Infrastructure.Persistence.Search;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class SearchRepository(
    ApplicationDbContext dbContext,
    IOptions<TopRatedOptions> topRatedOptions,
    ILogger<SearchRepository> logger,
    IMemoryCache? memoryCache = null,
    IOptions<NewReleasesOptions>? newReleasesOptions = null) : ISearchRepository
{
    private static readonly TimeSpan CatalogMeanCacheTtl = TimeSpan.FromMinutes(10);
    public async Task<PaginatedResult<SearchItem>> SearchAsync(
        SearchCriteria criteria,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var queryMatch = SearchQueryMatch.FromQuery(criteria.Query);
        var normalizedQuery = queryMatch.IsEmpty ? null : queryMatch.Primary;
        var rankingScope = CatalogSearchTitleLanguageScope.FromContentLocale(contentLocale);

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(
            dbContext,
            criteria,
            queryMatch,
            libraryScope: null,
            rankingScope);

        var page = criteria.Page;
        SearchKeysetCursor? keysetCursor = null;
        var isCursorContinuation = !string.IsNullOrWhiteSpace(criteria.Cursor);
        if (isCursorContinuation)
        {
            if (!SearchKeysetCursor.TryDecode(criteria.Cursor, criteria, normalizedQuery, out keysetCursor, out _))
            {
                throw new InvalidOperationException("Search cursor was not validated before repository execution.");
            }

            page = keysetCursor!.Page + 1;
        }

        var useSnapshotTotal = isCursorContinuation
            && keysetCursor!.Version >= 2
            && keysetCursor.SnapshotTotalCount > 0;

        var countExecuted = false;
        long countMs = 0;
        int totalCount;
        if (useSnapshotTotal)
        {
            totalCount = keysetCursor!.SnapshotTotalCount;
        }
        else
        {
            var countStopwatch = Stopwatch.StartNew();
            totalCount = await combinedQuery.CountAsync(cancellationToken);
            countMs = countStopwatch.ElapsedMilliseconds;
            countExecuted = true;
        }

        var sortedQuery = SearchQueryBuilder.ApplySort(combinedQuery, criteria.Sort, queryMatch);
        var pageQuery = keysetCursor is not null
            ? SearchKeysetPagination.ApplyAfterCursor(sortedQuery, keysetCursor, criteria.Sort, queryMatch)
            : sortedQuery.Skip((page - 1) * criteria.PageSize);

        var pageFetchStopwatch = Stopwatch.StartNew();
        var fetchedRows = await pageQuery
            .Take(criteria.PageSize + 1)
            .ToListAsync(cancellationToken);
        var pageFetchMs = pageFetchStopwatch.ElapsedMilliseconds;

        var hasNextPage = fetchedRows.Count > criteria.PageSize;
        var items = fetchedRows.Take(criteria.PageSize).ToList();

        string? nextCursor = null;
        if (hasNextPage)
        {
            var lastProjection = items[^1];
            var lastItem = ToSearchItem(lastProjection);
            nextCursor = SearchKeysetCursor.Encode(
                SearchKeysetCursor.CreateFromItem(
                    lastItem,
                    criteria,
                    normalizedQuery,
                    page,
                    totalCount,
                    lastProjection.RelevanceTier));
        }

        var mode = isCursorContinuation
            ? "CursorContinuation"
            : criteria.Page > 1
                ? "Offset"
                : "CursorFirst";

        SearchRepositoryLogMessages.LogSearchPaginationPerf(
            logger,
            mode,
            countExecuted,
            countMs,
            pageFetchMs,
            totalStopwatch.ElapsedMilliseconds,
            criteria.PageSize,
            items.Count,
            hasNextPage);

        return ToPaginatedResult(
            items,
            page,
            criteria.PageSize,
            totalCount,
            nextCursor,
            hasNextPage);
    }

    public async Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
        string query,
        int limit,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var queryMatch = SearchQueryMatch.FromQuery(query);
        var rankingScope = CatalogSearchTitleLanguageScope.FromContentLocale(contentLocale);
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

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(
            dbContext,
            searchCriteria,
            queryMatch,
            libraryScope: null,
            rankingScope);

        var projections = await SearchQueryBuilder
            .ApplyRelevanceSort(combinedQuery, queryMatch)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return projections
            .Select(item => new SearchSuggestion(
                item.Id,
                item.Type,
                item.Title,
                item.PosterUrl,
                item.TmdbId,
                item.KnownForDepartment))
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

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(
            dbContext,
            searchCriteria,
            SearchQueryMatch.Empty,
            titlesOnly: true);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplySort(combinedQuery, SearchSortOption.Popular, SearchQueryMatch.Empty)
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

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(
            dbContext,
            searchCriteria,
            SearchQueryMatch.Empty,
            titlesOnly: true);
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
        var configured = newReleasesOptions?.Value;
        var maxAgeDays = Math.Max(
            0,
            configured?.MaxAgeDays ?? NewReleasesOptions.DefaultMaxAgeDays);
        var minVoteCountMovie = Math.Max(
            0,
            configured?.MinVoteCountMovie ?? NewReleasesOptions.DefaultMinVoteCountMovie);
        var minVoteCountTv = Math.Max(
            0,
            configured?.MinVoteCountTv ?? NewReleasesOptions.DefaultMinVoteCountTv);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var combinedQuery = SearchQueryBuilder.BuildNewReleasesQuery(
            dbContext,
            criteria,
            today,
            maxAgeDays,
            minVoteCountMovie,
            minVoteCountTv);
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
        var combinedQuery = SearchQueryBuilder.BuildTopRatedQuery(
            dbContext,
            criteria,
            topRatedOptions.Value.MinVoteCountMovie,
            topRatedOptions.Value.MinVoteCountTv);
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

    public async Task<PaginatedResult<SearchItem>> GetHiddenGemsAsync(
        DiscoverBrowseCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = CatalogTitleListQuery.BuildHiddenGemsQuery(dbContext, criteria);
        var totalCount = await query.CountAsync(cancellationToken);
        var catalogMean = await ComputeEligiblePopulationMeanVoteAverageAsync(query, cancellationToken);
        var minimumVoteConfidence = HiddenGemsPolicy.MinimumVoteConfidence;
        var skip = (criteria.Page - 1) * criteria.PageSize;

        List<SearchItemProjection> items;
        if (UsesDatabaseTopRatedRanking())
        {
            items = await SearchQueryBuilder
                .ApplyHiddenGemsSort(query, catalogMean, minimumVoteConfidence)
                .Skip(skip)
                .Take(criteria.PageSize)
                .ToListAsync(cancellationToken);
        }
        else
        {
            var rankedItems = await query.ToListAsync(cancellationToken);
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

    public async Task<PaginatedResult<SearchItem>> GetFilteredTopRatedAsync(
        DiscoverBrowseCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = CatalogTitleListQuery.BuildFilteredTopRatedQuery(
            dbContext,
            criteria,
            topRatedOptions.Value.MinVoteCountMovie,
            topRatedOptions.Value.MinVoteCountTv);
        var totalCount = await query.CountAsync(cancellationToken);
        var catalogMean = await GetCatalogMeanVoteAverageAsync(criteria.Type, cancellationToken);
        var minimumVoteConfidence = topRatedOptions.Value.MinimumVoteConfidence;
        var skip = (criteria.Page - 1) * criteria.PageSize;

        List<SearchItemProjection> items;
        if (UsesDatabaseTopRatedRanking())
        {
            items = await SearchQueryBuilder
                .ApplyTopRatedSort(query, catalogMean, minimumVoteConfidence)
                .Skip(skip)
                .Take(criteria.PageSize)
                .ToListAsync(cancellationToken);
        }
        else
        {
            var rankedItems = await query.ToListAsync(cancellationToken);
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

    private static async Task<PaginatedResult<SearchItem>> PageTitleListAsync(
        IQueryable<SearchItemProjection> query,
        int page,
        int pageSize,
        Func<IQueryable<SearchItemProjection>, IQueryable<SearchItemProjection>> sort,
        CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await sort(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, page, pageSize, totalCount);
    }

    private bool UsesDatabaseTopRatedRanking() =>
        dbContext.Database.IsRelational() &&
        !string.Equals(
            dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

    private static async Task<decimal> ComputeEligiblePopulationMeanVoteAverageAsync(
        IQueryable<SearchItemProjection> query,
        CancellationToken cancellationToken)
    {
        var totals = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalVotes = group.Sum(item => (long)item.VoteCount),
                WeightedRatingSum = group.Sum(item => (double)item.VoteAverage * item.VoteCount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (totals is null || totals.TotalVotes <= 0)
        {
            return 7.0m;
        }

        return (decimal)(totals.WeightedRatingSum / totals.TotalVotes);
    }

    public async Task<decimal> GetCatalogMeanVoteAverageAsync(
        SearchContentType type,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"search:catalog-mean:{(int)type}";
        if (memoryCache?.TryGetValue(cacheKey, out decimal cachedMean) == true)
        {
            return cachedMean;
        }

        var mean = await ComputeCatalogMeanVoteAverageAsync(type, cancellationToken);
        memoryCache?.Set(cacheKey, mean, CatalogMeanCacheTtl);
        return mean;
    }

    private async Task<decimal> ComputeCatalogMeanVoteAverageAsync(
        SearchContentType type,
        CancellationToken cancellationToken)
    {
        var movieVotes = dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.VoteCount > 0)
            .Select(movie => new { movie.VoteAverage, movie.VoteCount });

        var tvVotes = dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.VoteCount > 0)
            .Select(tvShow => new { tvShow.VoteAverage, tvShow.VoteCount });

        var totalVotesQuery = type switch
        {
            SearchContentType.Movie => movieVotes.Select(item => (long)item.VoteCount),
            SearchContentType.Tv => tvVotes.Select(item => (long)item.VoteCount),
            _ => movieVotes.Select(item => (long)item.VoteCount)
                .Concat(tvVotes.Select(item => (long)item.VoteCount))
        };

        // Use double precision for the product so Npgsql does not promote VoteCount to
        // numeric(5,2) (VoteAverage scale), which overflows for VoteCount >= 1_000.
        var weightedRatingQuery = type switch
        {
            SearchContentType.Movie => movieVotes.Select(item => (double)item.VoteAverage * item.VoteCount),
            SearchContentType.Tv => tvVotes.Select(item => (double)item.VoteAverage * item.VoteCount),
            _ => movieVotes.Select(item => (double)item.VoteAverage * item.VoteCount)
                .Concat(tvVotes.Select(item => (double)item.VoteAverage * item.VoteCount))
        };

        var totalVotes = await totalVotesQuery.SumAsync(cancellationToken);
        if (totalVotes <= 0)
        {
            return 6.0m;
        }

        var weightedRating = await weightedRatingQuery.SumAsync(cancellationToken);
        return (decimal)(weightedRating / totalVotes);
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

    public async Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithAnyGenreAsync(
        IReadOnlyList<SearchItem> items,
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
                .Where(movieGenre => movieIds.Contains(movieGenre.MovieId))
                .Select(movieGenre => movieGenre.MovieId)
                .Distinct()
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
                .Where(tvShowGenre => tvShowIds.Contains(tvShowGenre.TvShowId))
                .Select(tvShowGenre => tvShowGenre.TvShowId)
                .Distinct()
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

        var combinedQuery = SearchQueryBuilder.BuildCombinedQuery(
            dbContext,
            searchCriteria,
            SearchQueryMatch.Empty,
            libraryScope: null,
            rankingScope: null,
            genreName: genreName,
            titlesOnly: true);
        var totalCount = await combinedQuery.CountAsync(cancellationToken);

        var items = await SearchQueryBuilder
            .ApplySort(combinedQuery, SearchSortOption.Popular, SearchQueryMatch.Empty)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(cancellationToken);

        return ToPaginatedResult(items, criteria.Page, criteria.PageSize, totalCount);
    }

    private static PaginatedResult<SearchItem> ToPaginatedResult(
        IReadOnlyList<SearchItemProjection> items,
        int page,
        int pageSize,
        int totalCount,
        string? nextCursor = null,
        bool? hasNextPageOverride = null)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<SearchItem>(
            items.Select(ToSearchItem).ToList(),
            page,
            pageSize,
            totalCount,
            totalPages,
            nextCursor,
            hasNextPageOverride);
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
            projection.Year,
            projection.TmdbId,
            projection.KnownForDepartment);
}
