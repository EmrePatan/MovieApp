using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Recommendations;
using MovieApp.Application.Recommendations;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence.Recommendations;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class RecommendationRepository(
    ApplicationDbContext dbContext,
    ILogger<RecommendationRepository>? repositoryLogger = null,
    IDbContextFactory<ApplicationDbContext>? dbContextFactory = null,
    IOptions<RecommendationOptions>? recommendationOptions = null) : IRecommendationRepository
{
    private const int MaxCastPeople = 20;
    private const int MaxStratifiedGenres = 8;
    private readonly ILogger<RecommendationRepository> _repositoryLogger =
        repositoryLogger ?? NullLogger<RecommendationRepository>.Instance;
    private readonly RecommendationOptions _recommendationOptions =
        recommendationOptions?.Value ?? new RecommendationOptions();
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
        int mildRatingMinScore = 6,
        CancellationToken cancellationToken = default) =>
        new UserRecommendationContextLoader(dbContext, dbContextFactory, _repositoryLogger).LoadAsync(
            userId,
            minimumInteractionsForEnrichment,
            mildRatingMinScore,
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
        var metrics = new RecommendationQueryMetrics();
        var candidates = new List<PersonalizedCandidateProfile>();
        long dbTotalMs = 0;
        long movieFetchMs = 0;
        long tvFetchMs = 0;
        var movieIdCount = 0;
        var tvIdCount = 0;
        var franchiseKeywordIds = await LoadFranchiseKeywordIdsAsync(metrics, cancellationToken);

        if (type is RecommendationContentType.All or RecommendationContentType.Movie)
        {
            var movieBudget = type == RecommendationContentType.Movie
                ? maxCandidates
                : RecommendationCandidateBudget.Split(maxCandidates, _recommendationOptions.CandidateMinorityTypeMinimum)
                    .MovieBudget;
            var movieCandidates = await LoadConsolidatedMovieCandidatesAsync(
                preferredGenreIds,
                excludedMovieIds,
                movieBudget,
                metrics,
                franchiseKeywordIds,
                cancellationToken);
            movieFetchMs = movieCandidates.ElapsedMs;
            dbTotalMs += movieFetchMs;
            movieIdCount = movieCandidates.Candidates.Count;
            candidates.AddRange(movieCandidates.Candidates);
        }

        if (type is RecommendationContentType.All or RecommendationContentType.Tv)
        {
            var tvBudget = maxCandidates;
            if (type == RecommendationContentType.All)
            {
                var split = RecommendationCandidateBudget.Split(
                    maxCandidates,
                    _recommendationOptions.CandidateMinorityTypeMinimum);
                var unusedMovieSlots = Math.Max(0, split.MovieBudget - movieIdCount);
                tvBudget = split.TvBudget + unusedMovieSlots;
            }

            var tvCandidates = await LoadConsolidatedTvCandidatesAsync(
                preferredGenreIds,
                excludedTvShowIds,
                tvBudget,
                metrics,
                franchiseKeywordIds,
                cancellationToken);
            tvFetchMs = tvCandidates.ElapsedMs;
            dbTotalMs += tvFetchMs;
            tvIdCount = tvCandidates.Candidates.Count;
            candidates.AddRange(tvCandidates.Candidates);
        }

        if (type == RecommendationContentType.All &&
            movieIdCount > 0 &&
            candidates.Count < maxCandidates)
        {
            var split = RecommendationCandidateBudget.Split(
                maxCandidates,
                _recommendationOptions.CandidateMinorityTypeMinimum);
            if (movieIdCount >= split.MovieBudget && tvIdCount < split.TvBudget)
            {
                var alreadySelectedMovieIds = candidates
                    .Where(candidate => candidate.Type == "movie")
                    .Select(candidate => candidate.Id)
                    .ToHashSet();
                var backfillExcluded = new HashSet<Guid>(excludedMovieIds);
                backfillExcluded.UnionWith(alreadySelectedMovieIds);
                var deficit = maxCandidates - candidates.Count;
                var backfill = await LoadConsolidatedMovieCandidatesAsync(
                    preferredGenreIds,
                    backfillExcluded,
                    deficit,
                    metrics,
                    franchiseKeywordIds,
                    cancellationToken);
                movieFetchMs += backfill.ElapsedMs;
                dbTotalMs += backfill.ElapsedMs;
                movieIdCount += backfill.Candidates.Count;
                candidates.AddRange(backfill.Candidates);
            }
        }

        LogCandidateFetchSummary(
            type.ToString(),
            metrics.DbRoundTrips,
            movieFetchMs,
            tvFetchMs,
            dbTotalMs,
            movieIdCount,
            tvIdCount,
            candidates.Count);

        return candidates;
    }

    private sealed record ConsolidatedCandidateLoadResult(
        List<PersonalizedCandidateProfile> Candidates,
        long ElapsedMs);

    private async Task<ConsolidatedCandidateLoadResult> LoadConsolidatedMovieCandidatesAsync(
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedMovieIds,
        int maxCandidates,
        RecommendationQueryMetrics metrics,
        IReadOnlySet<Guid> franchiseKeywordIds,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();

        var idSelectionStopwatch = Stopwatch.StartNew();
        var movieIds = await SelectMovieCandidateIdsAsync(
            preferredGenreIds,
            excludedMovieIds,
            maxCandidates,
            metrics,
            cancellationToken);
        idSelectionStopwatch.Stop();

        if (movieIds.Count == 0)
        {
            totalStopwatch.Stop();
            LogMovieCandidateFetchSummary(
                totalStopwatch.ElapsedMilliseconds,
                idSelectionStopwatch.ElapsedMilliseconds,
                0,
                0,
                metrics.DbRoundTrips,
                0,
                0);
            return new ConsolidatedCandidateLoadResult([], idSelectionStopwatch.ElapsedMilliseconds);
        }

        var hydrationStopwatch = Stopwatch.StartNew();
        metrics.RecordRoundTrip();
        var projections = await LoadPersonalizedMovieProjectionsAsync(movieIds, cancellationToken);
        hydrationStopwatch.Stop();

        var keywordStopwatch = Stopwatch.StartNew();
        metrics.RecordRoundTrip();
        var keywordIdsByMovieId = await LoadMovieKeywordIdsByCatalogIdsAsync(movieIds, cancellationToken);
        keywordStopwatch.Stop();

        var projectionById = projections.ToDictionary(projection => projection.Id);
        var candidates = movieIds
            .Where(movieId => projectionById.ContainsKey(movieId))
            .Select(movieId => RecommendationProjectionMapper.ToPersonalizedCandidateProfile(
                projectionById[movieId],
                keywordIdsByMovieId.GetValueOrDefault(movieId),
                franchiseKeywordIds))
            .ToList();

        totalStopwatch.Stop();

        LogMovieCandidateFetchSummary(
            totalStopwatch.ElapsedMilliseconds,
            idSelectionStopwatch.ElapsedMilliseconds,
            hydrationStopwatch.ElapsedMilliseconds,
            keywordStopwatch.ElapsedMilliseconds,
            metrics.DbRoundTrips,
            movieIds.Count,
            candidates.Count);

        return new ConsolidatedCandidateLoadResult(
            candidates,
            totalStopwatch.ElapsedMilliseconds);
    }

    private async Task<List<RecommendationCandidateProjection>> LoadPersonalizedMovieProjectionsAsync(
        List<Guid> movieIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.Movies
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
                TmdbCollectionId = movie.TmdbCollectionId
            })
            .ToListAsync(cancellationToken);
    }

    private void LogMovieCandidateFetchSummary(
        long totalMs,
        long idSelectionMs,
        long hydrationMs,
        long keywordMs,
        int dbRoundTrips,
        int candidateIdCount,
        int resultCount)
    {
        RecommendationRepositoryLogMessages.LogMovieCandidateFetch(
            _repositoryLogger,
            totalMs,
            idSelectionMs,
            hydrationMs,
            keywordMs,
            dbRoundTrips,
            candidateIdCount,
            resultCount);
    }

    private async Task<ConsolidatedCandidateLoadResult> LoadConsolidatedTvCandidatesAsync(
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedTvShowIds,
        int maxCandidates,
        RecommendationQueryMetrics metrics,
        IReadOnlySet<Guid> franchiseKeywordIds,
        CancellationToken cancellationToken)
    {
        if (maxCandidates == 0)
        {
            return new ConsolidatedCandidateLoadResult([], 0);
        }

        var stopwatch = Stopwatch.StartNew();
        var tvShowIds = await SelectTvCandidateIdsAsync(
            preferredGenreIds,
            excludedTvShowIds,
            maxCandidates,
            metrics,
            cancellationToken);
        if (tvShowIds.Count == 0)
        {
            stopwatch.Stop();
            return new ConsolidatedCandidateLoadResult([], stopwatch.ElapsedMilliseconds);
        }

        metrics.RecordRoundTrip();
        var rows = await BuildTvCandidateQuery(preferredGenreIds, excludedTvShowIds)
            .Where(tvShow => tvShowIds.Contains(tvShow.Id))
            .AsSplitQuery()
            .Select(tvShow => new PersonalizedCandidateQueryRow(
                new RecommendationCandidateProjection
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
                },
                tvShow.TvShowKeywords.Select(keyword => keyword.KeywordId).ToList()))
            .ToListAsync(cancellationToken);
        stopwatch.Stop();

        var rowById = rows.ToDictionary(row => row.Projection.Id);
        var candidates = tvShowIds
            .Where(rowById.ContainsKey)
            .Select(tvShowId => RecommendationProjectionMapper.ToPersonalizedCandidateProfile(
                rowById[tvShowId].Projection,
                rowById[tvShowId].KeywordIds,
                franchiseKeywordIds))
            .ToList();

        return new ConsolidatedCandidateLoadResult(candidates, stopwatch.ElapsedMilliseconds);
    }

    private sealed record PersonalizedCandidateQueryRow(
        RecommendationCandidateProjection Projection,
        List<Guid> KeywordIds);

    private async Task<List<Guid>> SelectMovieCandidateIdsAsync(
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedMovieIds,
        int maxCandidates,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (maxCandidates <= 0)
        {
            return [];
        }

        var genres = preferredGenreIds
            .Where(genreId => genreId != Guid.Empty)
            .Distinct()
            .Take(MaxStratifiedGenres)
            .ToList();
        if (genres.Count < 2)
        {
            return await TakeMovieIdsAsync(
                preferredGenreIds,
                excludedMovieIds,
                maxCandidates,
                metrics,
                cancellationToken);
        }

        var perGenre = Math.Min(
            maxCandidates,
            Math.Max(_recommendationOptions.CandidateMinPerGenre, maxCandidates / genres.Count));
        var buckets = new List<IReadOnlyList<Guid>>(genres.Count);
        foreach (var genreId in genres)
        {
            buckets.Add(await TakeMovieIdsAsync(
                [genreId],
                excludedMovieIds,
                perGenre,
                metrics,
                cancellationToken));
        }

        var selected = RecommendationCandidateBudget.RoundRobin(buckets, maxCandidates);
        if (selected.Count >= maxCandidates)
        {
            return selected;
        }

        var fill = await TakeMovieIdsAsync(
            preferredGenreIds,
            excludedMovieIds,
            maxCandidates,
            metrics,
            cancellationToken);
        return RecommendationCandidateBudget.AppendDistinct(selected, fill, maxCandidates);
    }

    private async Task<List<Guid>> TakeMovieIdsAsync(
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedMovieIds,
        int maxCandidates,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        metrics.RecordRoundTrip();
        return await BuildMovieCandidateQuery(preferredGenreIds, excludedMovieIds)
            .OrderByDescending(movie => movie.VoteCount)
            .ThenByDescending(movie => movie.VoteAverage)
            .ThenBy(movie => movie.Id)
            .Select(movie => movie.Id)
            .Take(maxCandidates)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> SelectTvCandidateIdsAsync(
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedTvShowIds,
        int maxCandidates,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (maxCandidates <= 0)
        {
            return [];
        }

        var genres = preferredGenreIds
            .Where(genreId => genreId != Guid.Empty)
            .Distinct()
            .Take(MaxStratifiedGenres)
            .ToList();
        if (genres.Count < 2)
        {
            return await TakeTvIdsAsync(
                preferredGenreIds,
                excludedTvShowIds,
                maxCandidates,
                metrics,
                cancellationToken);
        }

        var perGenre = Math.Min(
            maxCandidates,
            Math.Max(_recommendationOptions.CandidateMinPerGenre, maxCandidates / genres.Count));
        var buckets = new List<IReadOnlyList<Guid>>(genres.Count);
        foreach (var genreId in genres)
        {
            buckets.Add(await TakeTvIdsAsync(
                [genreId],
                excludedTvShowIds,
                perGenre,
                metrics,
                cancellationToken));
        }

        var selected = RecommendationCandidateBudget.RoundRobin(buckets, maxCandidates);
        if (selected.Count >= maxCandidates)
        {
            return selected;
        }

        var fill = await TakeTvIdsAsync(
            preferredGenreIds,
            excludedTvShowIds,
            maxCandidates,
            metrics,
            cancellationToken);
        return RecommendationCandidateBudget.AppendDistinct(selected, fill, maxCandidates);
    }

    private async Task<List<Guid>> TakeTvIdsAsync(
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedTvShowIds,
        int maxCandidates,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        metrics.RecordRoundTrip();
        return await BuildTvCandidateQuery(preferredGenreIds, excludedTvShowIds)
            .OrderByDescending(tvShow => tvShow.VoteCount)
            .ThenByDescending(tvShow => tvShow.VoteAverage)
            .ThenBy(tvShow => tvShow.Id)
            .Select(tvShow => tvShow.Id)
            .Take(maxCandidates)
            .ToListAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> LoadFranchiseKeywordIdsAsync(
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        var names = _recommendationOptions.DiversityFranchiseKeywordNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        if (names.Length == 0)
        {
            return [];
        }

        metrics.RecordRoundTrip();
        if (dbContext.Database.IsRelational() &&
            dbContext.Database.ProviderName is "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            IQueryable<Domain.Entities.Keyword>? union = null;
            foreach (var name in names)
            {
                var term = name;
                var branch = dbContext.Keywords
                    .AsNoTracking()
                    .Where(keyword => EF.Functions.ILike(keyword.Name, term));
                union = union is null ? branch : union.Union(branch);
            }

            var relationalIds = await union!
                .Select(keyword => keyword.Id)
                .ToListAsync(cancellationToken);
            return relationalIds.ToHashSet();
        }

        var keywords = await dbContext.Keywords
            .AsNoTracking()
            .Select(keyword => new { keyword.Id, keyword.Name })
            .ToListAsync(cancellationToken);
        var nameSet = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return keywords
            .Where(keyword => nameSet.Contains(keyword.Name))
            .Select(keyword => keyword.Id)
            .ToHashSet();
    }

    private IQueryable<Domain.Entities.Movie> BuildMovieCandidateQuery(
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedMovieIds)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = dbContext.Movies.AsNoTracking().AsQueryable();

        query = query.Where(movie => movie.ReleaseDate == null || movie.ReleaseDate <= today);

        if (_recommendationOptions.CandidateMinVoteCount > 0)
        {
            query = query.Where(movie => movie.VoteCount >= _recommendationOptions.CandidateMinVoteCount);
        }

        if (preferredGenreIds.Count > 0)
        {
            query = query.Where(movie => movie.MovieGenres.Any(genre => preferredGenreIds.Contains(genre.GenreId)));
        }

        if (excludedMovieIds.Count > 0)
        {
            query = query.Where(movie => !excludedMovieIds.Contains(movie.Id));
        }

        return query;
    }

    private IQueryable<Domain.Entities.TvShow> BuildTvCandidateQuery(
        IReadOnlyList<Guid> preferredGenreIds,
        IReadOnlySet<Guid> excludedTvShowIds)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = dbContext.TvShows.AsNoTracking().AsQueryable();

        query = query.Where(tvShow => tvShow.FirstAirDate == null || tvShow.FirstAirDate <= today);

        if (_recommendationOptions.CandidateMinVoteCount > 0)
        {
            query = query.Where(tvShow => tvShow.VoteCount >= _recommendationOptions.CandidateMinVoteCount);
        }

        if (preferredGenreIds.Count > 0)
        {
            query = query.Where(tvShow => tvShow.TvShowGenres.Any(genre => preferredGenreIds.Contains(genre.GenreId)));
        }

        if (excludedTvShowIds.Count > 0)
        {
            query = query.Where(tvShow => !excludedTvShowIds.Contains(tvShow.Id));
        }

        return query;
    }

    private void LogCandidateFetchSummary(
        string contentType,
        int dbRoundTrips,
        long movieFetchMs,
        long tvFetchMs,
        long dbTotalMs,
        int movieIdCount,
        int tvIdCount,
        int candidateCount)
    {
        RecommendationRepositoryLogMessages.LogCandidateFetch(
            _repositoryLogger,
            dbRoundTrips,
            movieFetchMs,
            tvFetchMs,
            dbTotalMs,
            movieIdCount,
            tvIdCount,
            candidateCount,
            contentType);
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
