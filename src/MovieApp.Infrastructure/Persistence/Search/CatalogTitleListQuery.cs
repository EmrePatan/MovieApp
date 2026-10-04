using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Search;

internal static class CatalogTitleListQuery
{
    public static IQueryable<SearchItemProjection> BuildHiddenGemsQuery(
        ApplicationDbContext dbContext,
        DiscoverBrowseCriteria criteria)
    {
        var movies = ApplyMovieFilters(
            dbContext.Movies.AsNoTracking().Where(movie =>
                movie.VoteAverage >= HiddenGemsPolicy.MinimumVoteAverage
                && movie.VoteCount >= HiddenGemsPolicy.MovieMinimumVoteCount
                && movie.VoteCount <= HiddenGemsPolicy.MovieMaximumVoteCount
                && movie.PosterPath != null
                && movie.PosterPath != string.Empty),
            criteria);

        var tvShows = ApplyTvFilters(
            dbContext.TvShows.AsNoTracking().Where(tvShow =>
                tvShow.VoteAverage >= HiddenGemsPolicy.MinimumVoteAverage
                && tvShow.VoteCount >= HiddenGemsPolicy.TvMinimumVoteCount
                && tvShow.VoteCount <= HiddenGemsPolicy.TvMaximumVoteCount
                && tvShow.PosterPath != null
                && tvShow.PosterPath != string.Empty),
            criteria);

        return Combine(criteria.Type, movies, tvShows);
    }

    public static IQueryable<SearchItemProjection> BuildFilteredTopRatedQuery(
        ApplicationDbContext dbContext,
        DiscoverBrowseCriteria criteria,
        int minimumMovieVotes,
        int minimumTvVotes)
    {
        var movieVotes = criteria.MinVoteCount is int requested && requested > minimumMovieVotes
            ? requested
            : minimumMovieVotes;
        var tvVotes = criteria.MinVoteCount is int requestedTv && requestedTv > minimumTvVotes
            ? requestedTv
            : minimumTvVotes;

        var movies = ApplyMovieFilters(
            dbContext.Movies.AsNoTracking().Where(movie => movie.VoteCount >= movieVotes),
            criteria);
        var tvShows = ApplyTvFilters(
            dbContext.TvShows.AsNoTracking().Where(tvShow => tvShow.VoteCount >= tvVotes),
            criteria);

        return Combine(criteria.Type, movies, tvShows);
    }

    private static IQueryable<SearchItemProjection> Combine(
        SearchContentType type,
        IQueryable<Movie> movies,
        IQueryable<TvShow> tvShows) =>
        type switch
        {
            SearchContentType.Movie => ProjectMovies(movies),
            SearchContentType.Tv => ProjectTvShows(tvShows),
            _ => ProjectMovies(movies).Concat(ProjectTvShows(tvShows))
        };

    private static IQueryable<Movie> ApplyMovieFilters(
        IQueryable<Movie> query,
        DiscoverBrowseCriteria criteria)
    {
        foreach (var genreId in criteria.GenreIds)
        {
            var id = genreId;
            query = query.Where(movie => movie.MovieGenres.Any(genre => genre.GenreId == id));
        }

        if (criteria.Year.HasValue)
        {
            var year = criteria.Year.Value;
            query = query.Where(movie =>
                movie.ReleaseDate.HasValue && movie.ReleaseDate.Value.Year == year);
        }

        if (criteria.YearFrom.HasValue)
        {
            var yearFrom = criteria.YearFrom.Value;
            query = query.Where(movie =>
                movie.ReleaseDate.HasValue && movie.ReleaseDate.Value.Year >= yearFrom);
        }

        if (criteria.YearTo.HasValue)
        {
            var yearTo = criteria.YearTo.Value;
            query = query.Where(movie =>
                movie.ReleaseDate.HasValue && movie.ReleaseDate.Value.Year <= yearTo);
        }

        if (criteria.MinRating.HasValue)
        {
            var minRating = criteria.MinRating.Value;
            query = query.Where(movie => movie.VoteAverage >= minRating);
        }

        if (criteria.MinVoteCount.HasValue)
        {
            var minVoteCount = criteria.MinVoteCount.Value;
            query = query.Where(movie => movie.VoteCount >= minVoteCount);
        }

        if (criteria.MinRuntimeMinutes.HasValue)
        {
            var minRuntime = criteria.MinRuntimeMinutes.Value;
            query = query.Where(movie =>
                movie.RuntimeMinutes.HasValue && movie.RuntimeMinutes.Value >= minRuntime);
        }

        if (criteria.MaxRuntimeMinutes.HasValue)
        {
            var maxRuntime = criteria.MaxRuntimeMinutes.Value;
            query = query.Where(movie =>
                movie.RuntimeMinutes.HasValue && movie.RuntimeMinutes.Value <= maxRuntime);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Language))
        {
            var language = criteria.Language.Trim().ToLowerInvariant();
            query = query.Where(movie => movie.OriginalLanguage == language);
        }

        if (!string.IsNullOrWhiteSpace(criteria.OriginCountry))
        {
            var originCountry = criteria.OriginCountry.Trim().ToUpperInvariant();
            query = query.Where(movie => movie.PrimaryOriginCountryCode == originCountry);
        }

        if (criteria.KeywordIds.Count > 0)
        {
            var keywordIds = criteria.KeywordIds;
            query = query.Where(movie =>
                movie.MovieKeywords.Any(join =>
                    keywordIds.Contains(join.KeywordId) &&
                    join.Keyword.ClassificationStatus != Domain.Enums.KeywordClassificationStatus.Excluded));
        }

        return query;
    }

    private static IQueryable<TvShow> ApplyTvFilters(
        IQueryable<TvShow> query,
        DiscoverBrowseCriteria criteria)
    {
        if (criteria.MinRuntimeMinutes.HasValue || criteria.MaxRuntimeMinutes.HasValue)
        {
            return query.Where(_ => false);
        }

        foreach (var genreId in criteria.GenreIds)
        {
            var id = genreId;
            query = query.Where(tvShow => tvShow.TvShowGenres.Any(genre => genre.GenreId == id));
        }

        if (criteria.Year.HasValue)
        {
            var year = criteria.Year.Value;
            query = query.Where(tvShow =>
                tvShow.FirstAirDate.HasValue && tvShow.FirstAirDate.Value.Year == year);
        }

        if (criteria.YearFrom.HasValue)
        {
            var yearFrom = criteria.YearFrom.Value;
            query = query.Where(tvShow =>
                tvShow.FirstAirDate.HasValue && tvShow.FirstAirDate.Value.Year >= yearFrom);
        }

        if (criteria.YearTo.HasValue)
        {
            var yearTo = criteria.YearTo.Value;
            query = query.Where(tvShow =>
                tvShow.FirstAirDate.HasValue && tvShow.FirstAirDate.Value.Year <= yearTo);
        }

        if (criteria.MinRating.HasValue)
        {
            var minRating = criteria.MinRating.Value;
            query = query.Where(tvShow => tvShow.VoteAverage >= minRating);
        }

        if (criteria.MinVoteCount.HasValue)
        {
            var minVoteCount = criteria.MinVoteCount.Value;
            query = query.Where(tvShow => tvShow.VoteCount >= minVoteCount);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Language))
        {
            var language = criteria.Language.Trim().ToLowerInvariant();
            query = query.Where(tvShow => tvShow.OriginalLanguage == language);
        }

        if (!string.IsNullOrWhiteSpace(criteria.OriginCountry))
        {
            var originCountry = criteria.OriginCountry.Trim().ToUpperInvariant();
            query = query.Where(tvShow => tvShow.PrimaryOriginCountryCode == originCountry);
        }

        if (criteria.KeywordIds.Count > 0)
        {
            var keywordIds = criteria.KeywordIds;
            query = query.Where(tvShow =>
                tvShow.TvShowKeywords.Any(join =>
                    keywordIds.Contains(join.KeywordId) &&
                    join.Keyword.ClassificationStatus != Domain.Enums.KeywordClassificationStatus.Excluded));
        }

        if (criteria.TvStatuses.Count > 0)
        {
            var statuses = criteria.TvStatuses.Select(MapStatus).ToList();
            query = query.Where(tvShow => statuses.Contains(tvShow.Status));
        }

        return query;
    }

    private static TvShowStatus MapStatus(TvDiscoverStatus status) =>
        status switch
        {
            TvDiscoverStatus.ReturningSeries => TvShowStatus.ReturningSeries,
            TvDiscoverStatus.Planned => TvShowStatus.Planned,
            TvDiscoverStatus.InProduction => TvShowStatus.InProduction,
            TvDiscoverStatus.Ended => TvShowStatus.Ended,
            TvDiscoverStatus.Canceled => TvShowStatus.Canceled,
            TvDiscoverStatus.Pilot => TvShowStatus.Pilot,
            _ => TvShowStatus.ReturningSeries
        };

    private static IQueryable<SearchItemProjection> ProjectMovies(IQueryable<Movie> query) =>
        query.Select(movie => new SearchItemProjection
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
            KnownForDepartment = null,
            RelevanceTier = 0
        });

    private static IQueryable<SearchItemProjection> ProjectTvShows(IQueryable<TvShow> query) =>
        query.Select(tvShow => new SearchItemProjection
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
            KnownForDepartment = null,
            RelevanceTier = 0
        });
}
