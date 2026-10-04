using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Common;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Search;

internal static class SearchQueryBuilder
{
    public static IQueryable<SearchItemProjection> BuildMovieQuery(
        ApplicationDbContext dbContext,
        SearchCriteria criteria,
        SearchQueryMatch queryMatch,
        CatalogSearchTitleLanguageScope? libraryScope = null,
        CatalogSearchTitleLanguageScope? rankingScope = null,
        string? genreName = null)
    {
        var query = dbContext.Movies.AsNoTracking().AsQueryable();

        if (!queryMatch.IsEmpty)
        {
            query = SearchCatalogContentQuery.WhereMovieMatchesSearch(
                query,
                dbContext,
                queryMatch,
                libraryScope);
        }

        if (criteria.GenreId.HasValue)
        {
            query = query.Where(movie =>
                movie.MovieGenres.Any(genre => genre.GenreId == criteria.GenreId.Value));
        }

        if (!string.IsNullOrWhiteSpace(genreName))
        {
            query = query.Where(movie =>
                movie.MovieGenres.Any(movieGenre => EF.Functions.ILike(movieGenre.Genre.Name, genreName)));
        }

        if (criteria.Year.HasValue)
        {
            query = query.Where(movie =>
                movie.ReleaseDate.HasValue && movie.ReleaseDate.Value.Year == criteria.Year.Value);
        }

        if (criteria.MinRating.HasValue)
        {
            query = query.Where(movie => movie.VoteAverage >= criteria.MinRating.Value);
        }

        if (criteria.MaxRating.HasValue)
        {
            query = query.Where(movie => movie.VoteAverage <= criteria.MaxRating.Value);
        }

        if (queryMatch.IsEmpty)
        {
            return query.Select(movie => new SearchItemProjection
            {
                Id = movie.Id,
                Type = "movie",
                Title = movie.Title,
                OriginalTitle = movie.OriginalTitle,
                Overview = movie.Overview,
                PosterUrl = movie.PosterPath,
                BackdropUrl = movie.BackdropPath,
                ReleaseDate = movie.ReleaseDate,
                VoteAverage = movie.VoteAverage,
                VoteCount = movie.VoteCount,
                Year = movie.ReleaseDate.HasValue ? movie.ReleaseDate.Value.Year : null,
                TmdbId = movie.TmdbId,
                KnownForDepartment = (string?)null,
                RelevanceTier = 0
            });
        }

        return SearchCatalogContentQuery.ProjectMoviesWithRelevance(
            query,
            dbContext,
            queryMatch.Primary,
            queryMatch.TurkishAlternate,
            queryMatch.Folded,
            rankingScope ?? CatalogSearchTitleLanguageScope.FromContentLocale(SupportedContentLocales.Default));
    }

    public static IQueryable<SearchItemProjection> BuildPersonQuery(
        ApplicationDbContext dbContext,
        SearchCriteria criteria,
        SearchQueryMatch queryMatch)
    {
        var textMatch = queryMatch.Text;
        var query = dbContext.People.AsNoTracking().AsQueryable();

        query = SearchTitleFilter.WherePersonNameContains(query, textMatch);

        return query.Select(person => new SearchItemProjection
        {
            Id = person.Id,
            Type = "person",
            Title = person.Name,
            OriginalTitle = null,
            Overview = null,
            PosterUrl = person.ProfilePath,
            BackdropUrl = null,
            ReleaseDate = null,
            VoteAverage = 0,
            VoteCount = 0,
            Year = null,
            TmdbId = person.TmdbId,
            KnownForDepartment = null,
            RelevanceTier =
                (EF.Functions.ILike(person.Name, textMatch.Primary)
                    && person.Name.Length == textMatch.Primary.Length)
                || (textMatch.TurkishAlternate != null
                    && EF.Functions.ILike(person.Name, textMatch.TurkishAlternate)
                    && person.Name.Length == textMatch.TurkishAlternate.Length)
                    ? 0
                    : EF.Functions.ILike(person.Name, textMatch.Primary + "%")
                        || (textMatch.TurkishAlternate != null
                            && EF.Functions.ILike(person.Name, textMatch.TurkishAlternate + "%"))
                        ? 1
                        : 2
        });
    }

    public static IQueryable<SearchItemProjection> BuildTvShowQuery(
        ApplicationDbContext dbContext,
        SearchCriteria criteria,
        SearchQueryMatch queryMatch,
        CatalogSearchTitleLanguageScope? libraryScope = null,
        CatalogSearchTitleLanguageScope? rankingScope = null,
        string? genreName = null)
    {
        var query = dbContext.TvShows.AsNoTracking().AsQueryable();

        if (!queryMatch.IsEmpty)
        {
            query = SearchCatalogContentQuery.WhereTvShowMatchesSearch(
                query,
                dbContext,
                queryMatch,
                libraryScope);
        }

        if (criteria.GenreId.HasValue)
        {
            query = query.Where(tvShow =>
                tvShow.TvShowGenres.Any(genre => genre.GenreId == criteria.GenreId.Value));
        }

        if (!string.IsNullOrWhiteSpace(genreName))
        {
            query = query.Where(tvShow =>
                tvShow.TvShowGenres.Any(tvShowGenre => EF.Functions.ILike(tvShowGenre.Genre.Name, genreName)));
        }

        if (criteria.Year.HasValue)
        {
            query = query.Where(tvShow =>
                tvShow.FirstAirDate.HasValue && tvShow.FirstAirDate.Value.Year == criteria.Year.Value);
        }

        if (criteria.MinRating.HasValue)
        {
            query = query.Where(tvShow => tvShow.VoteAverage >= criteria.MinRating.Value);
        }

        if (criteria.MaxRating.HasValue)
        {
            query = query.Where(tvShow => tvShow.VoteAverage <= criteria.MaxRating.Value);
        }

        if (queryMatch.IsEmpty)
        {
            return query.Select(tvShow => new SearchItemProjection
            {
                Id = tvShow.Id,
                Type = "tv",
                Title = tvShow.Title,
                OriginalTitle = tvShow.OriginalTitle,
                Overview = tvShow.Overview,
                PosterUrl = tvShow.PosterPath,
                BackdropUrl = tvShow.BackdropPath,
                ReleaseDate = tvShow.FirstAirDate,
                VoteAverage = tvShow.VoteAverage,
                VoteCount = tvShow.VoteCount,
                Year = tvShow.FirstAirDate.HasValue ? tvShow.FirstAirDate.Value.Year : null,
                TmdbId = tvShow.TmdbId,
                KnownForDepartment = (string?)null,
                RelevanceTier = 0
            });
        }

        return SearchCatalogContentQuery.ProjectTvShowsWithRelevance(
            query,
            dbContext,
            queryMatch.Primary,
            queryMatch.TurkishAlternate,
            queryMatch.Folded,
            rankingScope ?? CatalogSearchTitleLanguageScope.FromContentLocale(SupportedContentLocales.Default));
    }

    public static IQueryable<SearchItemProjection> BuildCombinedQuery(
        ApplicationDbContext dbContext,
        SearchCriteria criteria,
        SearchQueryMatch queryMatch,
        CatalogSearchTitleLanguageScope? libraryScope = null,
        CatalogSearchTitleLanguageScope? rankingScope = null,
        string? genreName = null,
        bool titlesOnly = false)
    {
        return criteria.Type switch
        {
            SearchContentType.Movie => BuildMovieQuery(dbContext, criteria, queryMatch, libraryScope, rankingScope, genreName),
            SearchContentType.Tv => BuildTvShowQuery(dbContext, criteria, queryMatch, libraryScope, rankingScope, genreName),
            SearchContentType.Person => BuildPersonQuery(dbContext, criteria, queryMatch),
            _ when titlesOnly => BuildMovieQuery(dbContext, criteria, queryMatch, libraryScope, rankingScope, genreName)
                .Concat(BuildTvShowQuery(dbContext, criteria, queryMatch, libraryScope, rankingScope, genreName)),
            _ => BuildMovieQuery(dbContext, criteria, queryMatch, libraryScope, rankingScope, genreName)
                .Concat(BuildTvShowQuery(dbContext, criteria, queryMatch, libraryScope, rankingScope, genreName))
                .Concat(BuildPersonQuery(dbContext, criteria, queryMatch))
        };
    }

    public static IQueryable<SearchItemProjection> BuildNewReleasesQuery(
        ApplicationDbContext dbContext,
        DiscoveryCriteria criteria,
        DateOnly today,
        int maxAgeDays,
        int minVoteCountMovie = NewReleasesOptions.DefaultMinVoteCountMovie,
        int minVoteCountTv = NewReleasesOptions.DefaultMinVoteCountTv)
    {
        var searchCriteria = new SearchCriteria(
            null,
            criteria.Type,
            null,
            null,
            null,
            null,
            SearchSortOption.DateDesc,
            criteria.Page,
            criteria.PageSize);

        return criteria.Type switch
        {
            SearchContentType.Movie => ApplyNewReleasesConstraints(
                BuildMovieQuery(dbContext, searchCriteria, SearchQueryMatch.Empty),
                today,
                maxAgeDays,
                minVoteCountMovie),
            SearchContentType.Tv => ApplyNewReleasesConstraints(
                BuildTvShowQuery(dbContext, searchCriteria, SearchQueryMatch.Empty),
                today,
                maxAgeDays,
                minVoteCountTv),
            _ => ApplyNewReleasesConstraints(
                    BuildMovieQuery(dbContext, searchCriteria, SearchQueryMatch.Empty),
                    today,
                    maxAgeDays,
                    minVoteCountMovie)
                .Concat(ApplyNewReleasesConstraints(
                    BuildTvShowQuery(dbContext, searchCriteria, SearchQueryMatch.Empty),
                    today,
                    maxAgeDays,
                    minVoteCountTv))
        };
    }

    private static IQueryable<SearchItemProjection> ApplyNewReleasesConstraints(
        IQueryable<SearchItemProjection> query,
        DateOnly today,
        int maxAgeDays,
        int minVoteCount) =>
        ApplyNewReleasesVoteFloor(
            ApplyNewReleasesPosterRequirement(ApplyNewReleaseWindow(query, today, maxAgeDays)),
            minVoteCount);

    private static IQueryable<SearchItemProjection> ApplyNewReleasesPosterRequirement(
        IQueryable<SearchItemProjection> query) =>
        query.Where(item => item.PosterUrl != null && item.PosterUrl != string.Empty);

    private static IQueryable<SearchItemProjection> ApplyNewReleasesVoteFloor(
        IQueryable<SearchItemProjection> query,
        int minVoteCount)
    {
        if (minVoteCount <= 0)
        {
            return query;
        }

        return query.Where(item => item.VoteCount >= minVoteCount);
    }

    private static IQueryable<SearchItemProjection> ApplyNewReleaseWindow(
        IQueryable<SearchItemProjection> query,
        DateOnly today,
        int maxAgeDays)
    {
        var bounded = query.Where(item =>
            item.ReleaseDate.HasValue &&
            item.ReleaseDate <= today);

        if (maxAgeDays <= 0)
        {
            return bounded;
        }

        var earliest = today.AddDays(-maxAgeDays);
        return bounded.Where(item => item.ReleaseDate >= earliest);
    }

    public static IQueryable<SearchItemProjection> BuildTopRatedQuery(
        ApplicationDbContext dbContext,
        DiscoveryCriteria criteria,
        int minimumMovieVotes = DiscoverCatalogSortPolicy.CuratedTopRatedListMinimumVoteCount,
        int minimumTvVotes = DiscoverCatalogSortPolicy.TopRatedMinimumVoteCountTv)
    {
        var searchCriteria = new SearchCriteria(
            null,
            criteria.Type,
            null,
            null,
            null,
            null,
            SearchSortOption.RatingDesc,
            criteria.Page,
            criteria.PageSize);

        return criteria.Type switch
        {
            SearchContentType.Movie => BuildMovieQuery(dbContext, searchCriteria, SearchQueryMatch.Empty)
                .Where(item => item.VoteCount >= minimumMovieVotes),
            SearchContentType.Tv => BuildTvShowQuery(dbContext, searchCriteria, SearchQueryMatch.Empty)
                .Where(item => item.VoteCount >= minimumTvVotes),
            _ => BuildMovieQuery(dbContext, searchCriteria, SearchQueryMatch.Empty)
                .Where(item => item.VoteCount >= minimumMovieVotes)
                .Concat(BuildTvShowQuery(dbContext, searchCriteria, SearchQueryMatch.Empty)
                    .Where(item => item.VoteCount >= minimumTvVotes))
        };
    }

    public static IQueryable<SearchItemProjection> ApplySort(
        IQueryable<SearchItemProjection> query,
        SearchSortOption sort,
        SearchQueryMatch queryMatch)
    {
        var effectiveSort = sort == SearchSortOption.Rating ? SearchSortOption.RatingDesc : sort;

        return effectiveSort switch
        {
            SearchSortOption.RatingDesc => ApplyDeterministicTieBreak(
                query
                    .OrderByDescending(item => item.VoteAverage)
                    .ThenByDescending(item => item.VoteCount)),
            SearchSortOption.RatingAsc => ApplyDeterministicTieBreak(
                query
                    .OrderBy(item => item.VoteAverage)
                    .ThenBy(item => item.VoteCount)),
            SearchSortOption.DateDesc => ApplyDeterministicTieBreak(
                query
                    .OrderByDescending(item => item.ReleaseDate)
                    .ThenByDescending(item => item.VoteAverage)),
            SearchSortOption.DateAsc => ApplyDeterministicTieBreak(
                query
                    .OrderBy(item => item.ReleaseDate)
                    .ThenBy(item => item.VoteAverage)),
            SearchSortOption.TitleAsc => ApplyDeterministicTieBreak(query.OrderBy(item => item.Title)),
            SearchSortOption.TitleDesc => ApplyDeterministicTieBreak(query.OrderByDescending(item => item.Title)),
            SearchSortOption.Popular => ApplyDeterministicTieBreak(
                query
                    .OrderByDescending(item => item.VoteCount)
                    .ThenByDescending(item => item.VoteAverage)),
            _ => ApplyRelevanceSort(query, queryMatch)
        };
    }

    public static IQueryable<SearchItemProjection> ApplyRelevanceSort(
        IQueryable<SearchItemProjection> query,
        SearchQueryMatch queryMatch)
    {
        if (queryMatch.IsEmpty)
        {
            return ApplyDeterministicTieBreak(
                query
                    .OrderByDescending(item => item.VoteCount)
                    .ThenByDescending(item => item.VoteAverage));
        }

        var ordered = SearchTitleFilter.OrderByRelevance(query, queryMatch);
        return ApplyDeterministicTieBreak(
            ordered
                .ThenByDescending(item => item.VoteCount)
                .ThenByDescending(item => item.VoteAverage));
    }

    public static IQueryable<SearchItemProjection> ApplyTrendingSort(IQueryable<SearchItemProjection> query)
    {
        return ApplyDeterministicTieBreak(
            query
                .OrderByDescending(item => item.VoteCount)
                .ThenByDescending(item => item.VoteAverage)
                .ThenByDescending(item => item.ReleaseDate ?? DateOnly.MinValue));
    }

    private static IOrderedQueryable<SearchItemProjection> ApplyDeterministicTieBreak(
        IOrderedQueryable<SearchItemProjection> query) =>
        query
            .ThenBy(item => item.Type)
            .ThenBy(item => item.Id);

    public static IQueryable<SearchItemProjection> ApplyNewReleasesSort(IQueryable<SearchItemProjection> query)
    {
        return ApplyDeterministicTieBreak(
            query
                .OrderByDescending(item => item.ReleaseDate)
                .ThenBy(item => item.Title));
    }

    public static IQueryable<SearchItemProjection> ApplyHiddenGemsSort(
        IQueryable<SearchItemProjection> query,
        decimal catalogMeanVoteAverage,
        int minimumVoteConfidence) =>
        ApplyTopRatedSort(query, catalogMeanVoteAverage, minimumVoteConfidence);

    public static IQueryable<SearchItemProjection> ApplyTopRatedSort(
        IQueryable<SearchItemProjection> query,
        decimal catalogMeanVoteAverage,
        int minimumVoteConfidence)
    {
        // Use double precision for SQL ordering. PostgreSQL numeric(5,2) intermediates overflow
        // when vote_count is multiplied by vote_average during Bayesian score translation.
        var minimumVotes = (double)minimumVoteConfidence;
        var catalogMean = (double)catalogMeanVoteAverage;

        return ApplyDeterministicTieBreak(
            query
                .OrderByDescending(item =>
                    item.VoteCount / (item.VoteCount + minimumVotes) * (double)item.VoteAverage
                    + minimumVotes / (item.VoteCount + minimumVotes) * catalogMean)
                .ThenByDescending(item => item.VoteCount)
                .ThenBy(item => item.Title));
    }
}
