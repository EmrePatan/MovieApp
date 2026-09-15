using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Search;

internal static class SearchQueryBuilder
{
    public static IQueryable<SearchItemProjection> BuildMovieQuery(
        ApplicationDbContext dbContext,
        SearchCriteria criteria,
        string? normalizedQuery,
        string? genreName = null)
    {
        var query = dbContext.Movies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            query = query.Where(movie =>
                EF.Functions.ILike(movie.Title, $"%{normalizedQuery}%") ||
                (movie.OriginalTitle != null && EF.Functions.ILike(movie.OriginalTitle, $"%{normalizedQuery}%")));
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
            KnownForDepartment = (string?)null
        });
    }

    public static IQueryable<SearchItemProjection> BuildPersonQuery(
        ApplicationDbContext dbContext,
        SearchCriteria criteria,
        string? normalizedQuery)
    {
        var query = dbContext.People.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            query = query.Where(person =>
                EF.Functions.ILike(person.Name, $"%{normalizedQuery}%"));
        }

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
            KnownForDepartment = null
        });
    }

    public static IQueryable<SearchItemProjection> BuildTvShowQuery(
        ApplicationDbContext dbContext,
        SearchCriteria criteria,
        string? normalizedQuery,
        string? genreName = null)
    {
        var query = dbContext.TvShows.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            query = query.Where(tvShow =>
                EF.Functions.ILike(tvShow.Title, $"%{normalizedQuery}%") ||
                (tvShow.OriginalTitle != null && EF.Functions.ILike(tvShow.OriginalTitle, $"%{normalizedQuery}%")));
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
            KnownForDepartment = (string?)null
        });
    }

    public static IQueryable<SearchItemProjection> BuildCombinedQuery(
        ApplicationDbContext dbContext,
        SearchCriteria criteria,
        string? normalizedQuery,
        string? genreName = null)
    {
        return criteria.Type switch
        {
            SearchContentType.Movie => BuildMovieQuery(dbContext, criteria, normalizedQuery, genreName),
            SearchContentType.Tv => BuildTvShowQuery(dbContext, criteria, normalizedQuery, genreName),
            SearchContentType.Person => BuildPersonQuery(dbContext, criteria, normalizedQuery),
            _ => BuildMovieQuery(dbContext, criteria, normalizedQuery, genreName)
                .Concat(BuildTvShowQuery(dbContext, criteria, normalizedQuery, genreName))
                .Concat(BuildPersonQuery(dbContext, criteria, normalizedQuery))
        };
    }

    public static IQueryable<SearchItemProjection> BuildNewReleasesQuery(
        ApplicationDbContext dbContext,
        DiscoveryCriteria criteria)
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
            SearchContentType.Movie => BuildMovieQuery(dbContext, searchCriteria, null)
                .Where(item => item.ReleaseDate.HasValue),
            SearchContentType.Tv => BuildTvShowQuery(dbContext, searchCriteria, null)
                .Where(item => item.ReleaseDate.HasValue),
            _ => BuildMovieQuery(dbContext, searchCriteria, null)
                .Where(item => item.ReleaseDate.HasValue)
                .Concat(BuildTvShowQuery(dbContext, searchCriteria, null)
                    .Where(item => item.ReleaseDate.HasValue))
        };
    }

    public static IQueryable<SearchItemProjection> BuildTopRatedQuery(
        ApplicationDbContext dbContext,
        DiscoveryCriteria criteria)
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
            SearchContentType.Movie => BuildMovieQuery(dbContext, searchCriteria, null)
                .Where(item => item.VoteCount > 0),
            SearchContentType.Tv => BuildTvShowQuery(dbContext, searchCriteria, null)
                .Where(item => item.VoteCount > 0),
            _ => BuildMovieQuery(dbContext, searchCriteria, null)
                .Where(item => item.VoteCount > 0)
                .Concat(BuildTvShowQuery(dbContext, searchCriteria, null)
                    .Where(item => item.VoteCount > 0))
        };
    }

    public static IQueryable<SearchItemProjection> ApplySort(
        IQueryable<SearchItemProjection> query,
        SearchSortOption sort,
        string? normalizedQuery)
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
            _ => ApplyRelevanceSort(query, normalizedQuery)
        };
    }

    public static IQueryable<SearchItemProjection> ApplyRelevanceSort(
        IQueryable<SearchItemProjection> query,
        string? normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return ApplyDeterministicTieBreak(
                query
                    .OrderByDescending(item => item.VoteAverage)
                    .ThenByDescending(item => item.VoteCount));
        }

        return ApplyDeterministicTieBreak(
            query
                .OrderBy(item =>
                    EF.Functions.ILike(item.Title, normalizedQuery) && item.Title.Length == normalizedQuery.Length
                        ? 0
                        : EF.Functions.ILike(item.Title, normalizedQuery + "%")
                            ? 1
                            : 2)
                .ThenByDescending(item => item.VoteAverage)
                .ThenByDescending(item => item.VoteCount));
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
