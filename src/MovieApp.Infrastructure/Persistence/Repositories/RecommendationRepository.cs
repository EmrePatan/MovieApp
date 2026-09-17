using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Recommendations;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class RecommendationRepository(
    ApplicationDbContext dbContext,
    ILogger<RecommendationRepository>? repositoryLogger = null) : IRecommendationRepository
{
    private const int MaxCastPeople = 20;
    private readonly ILogger<RecommendationRepository> _repositoryLogger =
        repositoryLogger ?? NullLogger<RecommendationRepository>.Instance;
    private readonly SimilarCandidateIdBatchLoader _similarCandidateIdBatchLoader = new(dbContext);

    public Task<bool> MovieExistsAsync(Guid movieId, CancellationToken cancellationToken = default) =>
        dbContext.Movies.AsNoTracking().AnyAsync(movie => movie.Id == movieId, cancellationToken);

    public Task<bool> TvShowExistsAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
        dbContext.TvShows.AsNoTracking().AnyAsync(tvShow => tvShow.Id == tvShowId, cancellationToken);

    public async Task<SimilaritySourceProfile?> GetMovieSimilarityProfileAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var projection = await GetMovieProjectionAsync(movieId, cancellationToken);
        return projection is null ? null : RecommendationProjectionMapper.ToSimilaritySourceProfile(projection);
    }

    public async Task<SimilaritySourceProfile?> GetTvShowSimilarityProfileAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var projection = await GetTvShowProjectionAsync(tvShowId, cancellationToken);
        return projection is null ? null : RecommendationProjectionMapper.ToSimilaritySourceProfile(projection);
    }

    public async Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarMovieCandidatesAsync(
        Guid sourceMovieId,
        IReadOnlyList<Guid> genreIds,
        int maxCandidates,
        CancellationToken cancellationToken = default)
    {
        if (genreIds.Count == 0)
        {
            return [];
        }

        var movieIds = await GetSimilarMovieCandidateIdsInternalAsync(
            sourceMovieId,
            genreIds,
            maxCandidates,
            cancellationToken);
        var projections = await LoadMovieProjectionsAsync(movieIds, cancellationToken);
        return projections.Select(RecommendationProjectionMapper.ToSimilarityCandidateProfile).ToList();
    }

    public async Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarTvShowCandidatesAsync(
        Guid sourceTvShowId,
        IReadOnlyList<Guid> genreIds,
        int maxCandidates,
        CancellationToken cancellationToken = default)
    {
        if (genreIds.Count == 0)
        {
            return [];
        }

        var tvShowIds = await GetSimilarTvShowCandidateIdsInternalAsync(
            sourceTvShowId,
            genreIds,
            maxCandidates,
            cancellationToken);
        var projections = await LoadTvShowProjectionsAsync(tvShowIds, cancellationToken);
        return projections.Select(RecommendationProjectionMapper.ToSimilarityCandidateProfile).ToList();
    }

    private async Task<List<Guid>> GetSimilarMovieCandidateIdsInternalAsync(
        Guid sourceMovieId,
        IReadOnlyList<Guid> genreIds,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        if (genreIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.Id != sourceMovieId &&
                            movie.MovieGenres.Any(genre => genreIds.Contains(genre.GenreId)))
            .OrderByDescending(movie => movie.VoteCount)
            .ThenByDescending(movie => movie.VoteAverage)
            .Select(movie => movie.Id)
            .Take(maxCandidates)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetSimilarTvShowCandidateIdsInternalAsync(
        Guid sourceTvShowId,
        IReadOnlyList<Guid> genreIds,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        if (genreIds.Count == 0)
        {
            return [];
        }

        return await dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => tvShow.Id != sourceTvShowId &&
                             tvShow.TvShowGenres.Any(genre => genreIds.Contains(genre.GenreId)))
            .OrderByDescending(tvShow => tvShow.VoteCount)
            .ThenByDescending(tvShow => tvShow.VoteAverage)
            .Select(tvShow => tvShow.Id)
            .Take(maxCandidates)
            .ToListAsync(cancellationToken);
    }

    public Task<UserRecommendationContext> GetUserRecommendationContextAsync(
        Guid userId,
        int minimumInteractionsForEnrichment = 0,
        CancellationToken cancellationToken = default) =>
        new UserRecommendationContextLoader(dbContext, _repositoryLogger).LoadAsync(
            userId,
            minimumInteractionsForEnrichment,
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, SimilaritySourceProfile>> GetMovieSimilarityProfilesAsync(
        IReadOnlyList<Guid> movieIds,
        CancellationToken cancellationToken = default)
    {
        if (movieIds.Count == 0)
        {
            return new Dictionary<Guid, SimilaritySourceProfile>();
        }

        var projections = await LoadMovieProjectionsAsync(movieIds.Distinct().ToList(), cancellationToken);
        return projections
            .Select(RecommendationProjectionMapper.ToSimilaritySourceProfile)
            .ToDictionary(profile => profile.Id);
    }

    public async Task<IReadOnlyDictionary<Guid, SimilaritySourceProfile>> GetTvShowSimilarityProfilesAsync(
        IReadOnlyList<Guid> tvShowIds,
        CancellationToken cancellationToken = default)
    {
        if (tvShowIds.Count == 0)
        {
            return new Dictionary<Guid, SimilaritySourceProfile>();
        }

        var projections = await LoadTvShowProjectionsAsync(tvShowIds.Distinct().ToList(), cancellationToken);
        return projections
            .Select(RecommendationProjectionMapper.ToSimilaritySourceProfile)
            .ToDictionary(profile => profile.Id);
    }

    public async Task<IReadOnlyList<Guid>> GetSimilarMovieCandidateIdsAsync(
        Guid sourceMovieId,
        IReadOnlyList<Guid> genreIds,
        int maxCandidates,
        CancellationToken cancellationToken = default) =>
        await GetSimilarMovieCandidateIdsInternalAsync(sourceMovieId, genreIds, maxCandidates, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetSimilarTvShowCandidateIdsAsync(
        Guid sourceTvShowId,
        IReadOnlyList<Guid> genreIds,
        int maxCandidates,
        CancellationToken cancellationToken = default) =>
        await GetSimilarTvShowCandidateIdsInternalAsync(sourceTvShowId, genreIds, maxCandidates, cancellationToken);

    public async Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarMovieCandidatesByIdsAsync(
        IReadOnlyList<Guid> movieIds,
        CancellationToken cancellationToken = default)
    {
        var projections = await LoadMovieProjectionsAsync(movieIds.Distinct().ToList(), cancellationToken);
        return projections.Select(RecommendationProjectionMapper.ToSimilarityCandidateProfile).ToList();
    }

    public async Task<IReadOnlyList<SimilarityCandidateProfile>> GetSimilarTvShowCandidatesByIdsAsync(
        IReadOnlyList<Guid> tvShowIds,
        CancellationToken cancellationToken = default)
    {
        var projections = await LoadTvShowProjectionsAsync(tvShowIds.Distinct().ToList(), cancellationToken);
        return projections.Select(RecommendationProjectionMapper.ToSimilarityCandidateProfile).ToList();
    }

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSimilarMovieCandidateIdsForSourcesAsync(
        IReadOnlyList<SimilaritySourceGenreRequest> sources,
        int maxCandidates,
        CancellationToken cancellationToken = default) =>
        _similarCandidateIdBatchLoader.LoadMovieCandidateIdsAsync(sources, maxCandidates, cancellationToken);

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetSimilarTvShowCandidateIdsForSourcesAsync(
        IReadOnlyList<SimilaritySourceGenreRequest> sources,
        int maxCandidates,
        CancellationToken cancellationToken = default) =>
        _similarCandidateIdBatchLoader.LoadTvShowCandidateIdsAsync(sources, maxCandidates, cancellationToken);

    public async Task<IReadOnlyList<PersonalizedCandidateProfile>> GetPersonalizedCandidatesAsync(
        RecommendationContentType type,
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedMovieIds,
        IReadOnlySet<Guid> excludedTvShowIds,
        int maxCandidates,
        CancellationToken cancellationToken = default)
    {
        var candidates = new List<PersonalizedCandidateProfile>();
        long dbTotalMs = 0;

        List<Guid> movieIds = [];
        List<Guid> tvShowIds = [];
        long movieIdsDbMs = 0;
        long movieProjectionsDbMs = 0;
        long tvIdsDbMs = 0;
        long tvProjectionsDbMs = 0;

        if (type is RecommendationContentType.All or RecommendationContentType.Movie)
        {
            var movieIdsStopwatch = Stopwatch.StartNew();
            movieIds = await GetCandidateMovieIdsAsync(preferredGenreIds, excludedMovieIds, maxCandidates, cancellationToken);
            movieIdsStopwatch.Stop();
            movieIdsDbMs = movieIdsStopwatch.ElapsedMilliseconds;
            dbTotalMs += movieIdsDbMs;

            var movieProjectionsStopwatch = Stopwatch.StartNew();
            var movieProjections = await LoadMovieProjectionsAsync(movieIds, cancellationToken);
            movieProjectionsStopwatch.Stop();
            movieProjectionsDbMs = movieProjectionsStopwatch.ElapsedMilliseconds;
            dbTotalMs += movieProjectionsDbMs;

            candidates.AddRange(movieProjections.Select(projection =>
                RecommendationProjectionMapper.ToPersonalizedCandidateProfile(projection)));
        }

        if (type is RecommendationContentType.All or RecommendationContentType.Tv)
        {
            var remaining = Math.Max(0, maxCandidates - candidates.Count);

            var tvIdsStopwatch = Stopwatch.StartNew();
            tvShowIds = await GetCandidateTvShowIdsAsync(preferredGenreIds, excludedTvShowIds, remaining, cancellationToken);
            tvIdsStopwatch.Stop();
            tvIdsDbMs = tvIdsStopwatch.ElapsedMilliseconds;
            dbTotalMs += tvIdsDbMs;

            var tvProjectionsStopwatch = Stopwatch.StartNew();
            var tvProjections = await LoadTvShowProjectionsAsync(tvShowIds, cancellationToken);
            tvProjectionsStopwatch.Stop();
            tvProjectionsDbMs = tvProjectionsStopwatch.ElapsedMilliseconds;
            dbTotalMs += tvProjectionsDbMs;

            candidates.AddRange(tvProjections.Select(projection =>
                RecommendationProjectionMapper.ToPersonalizedCandidateProfile(projection)));
        }

        if (candidates.Count == 0)
        {
            LogCandidateFetchSummary(
                type.ToString(),
                movieIdsDbMs,
                movieProjectionsDbMs,
                tvIdsDbMs,
                tvProjectionsDbMs,
                movieKeywordsDbMs: 0,
                tvKeywordsDbMs: 0,
                dbTotalMs,
                movieIds.Count,
                tvShowIds.Count,
                candidates.Count);

            return candidates;
        }

        var movieKeywordsStopwatch = Stopwatch.StartNew();
        var movieKeywordLookup = await LoadMovieKeywordIdsByCatalogIdsAsync(movieIds, cancellationToken);
        movieKeywordsStopwatch.Stop();
        var movieKeywordsDbMs = movieKeywordsStopwatch.ElapsedMilliseconds;
        dbTotalMs += movieKeywordsDbMs;

        var tvKeywordsStopwatch = Stopwatch.StartNew();
        var tvKeywordLookup = await LoadTvShowKeywordIdsByCatalogIdsAsync(tvShowIds, cancellationToken);
        tvKeywordsStopwatch.Stop();
        var tvKeywordsDbMs = tvKeywordsStopwatch.ElapsedMilliseconds;
        dbTotalMs += tvKeywordsDbMs;

        var enrichedCandidates = candidates
            .Select(candidate =>
            {
                var keywordIds = candidate.Type == "movie"
                    ? movieKeywordLookup.GetValueOrDefault(candidate.Id)
                    : tvKeywordLookup.GetValueOrDefault(candidate.Id);

                return keywordIds is null || keywordIds.Count == 0
                    ? candidate
                    : candidate with { KeywordIds = keywordIds };
            })
            .ToList();

        LogCandidateFetchSummary(
            type.ToString(),
            movieIdsDbMs,
            movieProjectionsDbMs,
            tvIdsDbMs,
            tvProjectionsDbMs,
            movieKeywordsDbMs,
            tvKeywordsDbMs,
            dbTotalMs,
            movieIds.Count,
            tvShowIds.Count,
            enrichedCandidates.Count);

        return enrichedCandidates;
    }

    private void LogCandidateFetchSummary(
        string contentType,
        long movieIdsDbMs,
        long movieProjectionsDbMs,
        long tvIdsDbMs,
        long tvProjectionsDbMs,
        long movieKeywordsDbMs,
        long tvKeywordsDbMs,
        long dbTotalMs,
        int movieIdCount,
        int tvIdCount,
        int candidateCount)
    {
        RecommendationRepositoryLogMessages.LogCandidateFetch(
            _repositoryLogger,
            movieIdsDbMs,
            movieProjectionsDbMs,
            tvIdsDbMs,
            tvProjectionsDbMs,
            movieKeywordsDbMs,
            tvKeywordsDbMs,
            dbTotalMs,
            movieIdCount,
            tvIdCount,
            candidateCount,
            contentType);
    }

    private async Task<List<Guid>> GetCandidateMovieIdsAsync(
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedMovieIds,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = dbContext.Movies.AsNoTracking().AsQueryable();

        query = query.Where(movie => movie.ReleaseDate == null || movie.ReleaseDate <= today);

        if (preferredGenreIds.Count > 0)
        {
            query = query.Where(movie => movie.MovieGenres.Any(genre => preferredGenreIds.Contains(genre.GenreId)));
        }

        if (excludedMovieIds.Count > 0)
        {
            query = query.Where(movie => !excludedMovieIds.Contains(movie.Id));
        }

        return await query
            .OrderByDescending(movie => movie.VoteCount)
            .ThenByDescending(movie => movie.VoteAverage)
            .Select(movie => movie.Id)
            .Take(maxCandidates)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetCandidateTvShowIdsAsync(
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedTvShowIds,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = dbContext.TvShows.AsNoTracking().AsQueryable();

        query = query.Where(tvShow => tvShow.FirstAirDate == null || tvShow.FirstAirDate <= today);

        if (preferredGenreIds.Count > 0)
        {
            query = query.Where(tvShow => tvShow.TvShowGenres.Any(genre => preferredGenreIds.Contains(genre.GenreId)));
        }

        if (excludedTvShowIds.Count > 0)
        {
            query = query.Where(tvShow => !excludedTvShowIds.Contains(tvShow.Id));
        }

        return await query
            .OrderByDescending(tvShow => tvShow.VoteCount)
            .ThenByDescending(tvShow => tvShow.VoteAverage)
            .Select(tvShow => tvShow.Id)
            .Take(maxCandidates)
            .ToListAsync(cancellationToken);
    }

    private async Task<RecommendationCandidateProjection?> GetMovieProjectionAsync(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        var projections = await LoadMovieProjectionsAsync([movieId], cancellationToken);
        return projections.FirstOrDefault();
    }

    private async Task<RecommendationCandidateProjection?> GetTvShowProjectionAsync(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        var projections = await LoadTvShowProjectionsAsync([tvShowId], cancellationToken);
        return projections.FirstOrDefault();
    }

    private async Task<List<RecommendationCandidateProjection>> LoadMovieProjectionsAsync(
        List<Guid> movieIds,
        CancellationToken cancellationToken)
    {
        if (movieIds.Count == 0)
        {
            return [];
        }

        var movies = await dbContext.Movies
            .AsNoTracking()
            .AsSplitQuery()
            .Where(movie => movieIds.Contains(movie.Id))
            .Select(movie => new RecommendationCandidateProjection
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
                GenreIds = movie.MovieGenres.Select(genre => genre.GenreId).ToList(),
                GenreNames = movie.MovieGenres.Select(genre => genre.Genre.Name).ToList(),
                PersonIds = movie.MoviePeople
                    .Where(person => person.CreditType == CreditType.Cast)
                    .OrderBy(person => person.PersonId)
                    .Select(person => person.PersonId)
                    .Take(MaxCastPeople)
                    .ToList(),
                TmdbCollectionId = movie.TmdbCollectionId
            })
            .ToListAsync(cancellationToken);

        return movies;
    }

    private async Task<List<RecommendationCandidateProjection>> LoadTvShowProjectionsAsync(
        List<Guid> tvShowIds,
        CancellationToken cancellationToken)
    {
        if (tvShowIds.Count == 0)
        {
            return [];
        }

        var tvShows = await dbContext.TvShows
            .AsNoTracking()
            .AsSplitQuery()
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .Select(tvShow => new RecommendationCandidateProjection
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
                GenreIds = tvShow.TvShowGenres.Select(genre => genre.GenreId).ToList(),
                GenreNames = tvShow.TvShowGenres.Select(genre => genre.Genre.Name).ToList(),
                PersonIds = tvShow.TvShowPeople
                    .Where(person => person.CreditType == CreditType.Cast)
                    .OrderBy(person => person.PersonId)
                    .Select(person => person.PersonId)
                    .Take(MaxCastPeople)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return tvShows;
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LoadMovieKeywordIdsByCatalogIdsAsync(
        List<Guid> movieIds,
        CancellationToken cancellationToken)
    {
        if (movieIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var rows = await dbContext.MovieKeywords
            .AsNoTracking()
            .Where(item => movieIds.Contains(item.MovieId))
            .Select(item => new { item.MovieId, item.KeywordId })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.MovieId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group.Select(row => row.KeywordId).Distinct().ToList());
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LoadTvShowKeywordIdsByCatalogIdsAsync(
        List<Guid> tvShowIds,
        CancellationToken cancellationToken)
    {
        if (tvShowIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var rows = await dbContext.TvShowKeywords
            .AsNoTracking()
            .Where(item => tvShowIds.Contains(item.TvShowId))
            .Select(item => new { item.TvShowId, item.KeywordId })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.TvShowId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group.Select(row => row.KeywordId).Distinct().ToList());
    }
}
