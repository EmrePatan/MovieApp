using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Application.Services.TvShows;
using MovieApp.Application.Validation;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.WatchHistory;

public sealed class WatchHistoryService(
    ICurrentUser currentUser,
    IWatchedMovieRepository watchedMovieRepository,
    IWatchedEpisodeRepository watchedEpisodeRepository,
    IMovieRepository movieRepository,
    IEpisodeRepository episodeRepository,
    ITvShowRepository tvShowRepository,
    ISeasonRepository seasonRepository,
    IGetSeasonService getSeasonService,
    ITvShowSeasonSummaryHydrator seasonSummaryHydrator,
    ITvShowCatalogSyncStateService catalogSyncStateService,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator,
    ILogger<WatchHistoryService> logger,
    IServiceScopeFactory? analyticsScopeFactory = null) : IWatchHistoryService
{
    public async Task<WatchMutationResult> MarkMovieWatchedAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var ensureStopwatch = Stopwatch.StartNew();
        await EnsureMovieExistsAsync(movieId, cancellationToken);
        ensureStopwatch.Stop();

        var utcNow = DateTime.UtcNow;
        var watchedMovie = WatchedMovie.Create(userId, movieId, utcNow);

        var upsertStopwatch = Stopwatch.StartNew();
        var (entity, created) = await watchedMovieRepository.UpsertAsync(watchedMovie, cancellationToken);
        upsertStopwatch.Stop();

        var analyticsStopwatch = Stopwatch.StartNew();
        await InvalidateProfileStatisticsAsync(userId, cancellationToken);
        analyticsStopwatch.Stop();

        totalStopwatch.Stop();
        WatchHistoryPerfLogMessages.LogMovieWatchState(
            logger,
            movieId,
            correlationId: null,
            totalStopwatch.ElapsedMilliseconds,
            ensureStopwatch.ElapsedMilliseconds,
            upsertStopwatch.ElapsedMilliseconds,
            created,
            analyticsStopwatch.ElapsedMilliseconds);

        return new WatchMutationResult(entity.WatchedAt, created);
    }

    public async Task UnmarkMovieWatchedAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await watchedMovieRepository.RemoveAsync(userId, movieId, cancellationToken);
        await InvalidateProfileStatisticsAsync(userId, cancellationToken);
    }

    public async Task<MovieWatchStatusResult> GetMovieWatchStatusAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var watchedMovie = await watchedMovieRepository.GetByUserAndMovieAsync(userId, movieId, cancellationToken);
        return watchedMovie is null
            ? new MovieWatchStatusResult(movieId, false, null)
            : new MovieWatchStatusResult(movieId, true, watchedMovie.WatchedAt);
    }

    public async Task<PaginatedResult<WatchedMovieResult>> GetWatchedMoviesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidatePagination(page, pageSize);

        var (items, totalCount) = await watchedMovieRepository.GetUserWatchedMoviesAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

        return WatchHistoryMapper.ToWatchedMoviesResult(items, page, pageSize, totalCount);
    }

    public async Task<WatchMutationResult> MarkEpisodeWatchedAsync(
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await EnsureEpisodeExistsAsync(episodeId, cancellationToken);

        var utcNow = DateTime.UtcNow;
        var watchedEpisode = WatchedEpisode.Create(userId, episodeId, utcNow);
        var (entity, created) = await watchedEpisodeRepository.UpsertAsync(watchedEpisode, cancellationToken);
        await InvalidateProfileStatisticsAsync(userId, cancellationToken);

        return new WatchMutationResult(entity.WatchedAt, created);
    }

    public async Task UnmarkEpisodeWatchedAsync(Guid episodeId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await watchedEpisodeRepository.RemoveAsync(userId, episodeId, cancellationToken);
        await InvalidateProfileStatisticsAsync(userId, cancellationToken);
    }

    public async Task<EpisodeWatchStatusResult> GetEpisodeWatchStatusAsync(
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var watchedEpisode = await watchedEpisodeRepository.GetByUserAndEpisodeAsync(userId, episodeId, cancellationToken);
        return watchedEpisode is null
            ? new EpisodeWatchStatusResult(episodeId, false, null)
            : new EpisodeWatchStatusResult(episodeId, true, watchedEpisode.WatchedAt);
    }

    public async Task<PaginatedResult<WatchedEpisodeResult>> GetWatchedEpisodesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidatePagination(page, pageSize);

        var (items, totalCount) = await watchedEpisodeRepository.GetUserWatchedEpisodesAsync(
            userId,
            page,
            pageSize,
            cancellationToken);

        return WatchHistoryMapper.ToWatchedEpisodesResult(items, page, pageSize, totalCount);
    }

    internal const int MaxRecentHistoryDepth = 500;

    public async Task<PaginatedResult<RecentWatchHistoryItemResult>> GetRecentWatchHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        ValidatePagination(page, pageSize);
        var fetchCount = GetRecentHistoryFetchCount(page, pageSize);

        var totalCount = await watchedMovieRepository.CountForUserAsync(userId, cancellationToken)
            + await watchedEpisodeRepository.CountForUserAsync(userId, cancellationToken);
        var recentMovies = await watchedMovieRepository.GetRecentForUserAsync(userId, fetchCount, cancellationToken);
        var recentEpisodes = await watchedEpisodeRepository.GetRecentForUserAsync(userId, fetchCount, cancellationToken);

        var merged = recentMovies
            .Select(movie => new RecentWatchHistoryItemResult(
                "movie",
                movie.MovieId,
                null,
                null,
                movie.Title,
                null,
                null,
                null,
                null,
                movie.WatchedAt))
            .Concat(recentEpisodes.Select(episode => new RecentWatchHistoryItemResult(
                "episode",
                null,
                episode.EpisodeId,
                episode.TvShowId,
                null,
                episode.TvShowTitle,
                episode.SeasonNumber,
                episode.EpisodeNumber,
                episode.EpisodeTitle,
                episode.WatchedAt)))
            .OrderByDescending(item => item.WatchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return WatchHistoryMapper.ToRecentWatchHistoryResult(merged, page, pageSize, totalCount);
    }

    public async Task<TvShowWatchProgressResult> GetTvShowWatchProgressAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var existenceStopwatch = Stopwatch.StartNew();
        var tvShowStatus = await tvShowRepository.GetStatusAsync(tvShowId, cancellationToken)
            ?? throw new NotFoundException("The requested TV show was not found.");
        existenceStopwatch.Stop();

        var progressStopwatch = Stopwatch.StartNew();
        var episodeCounts = await episodeRepository.GetEpisodeCountsBySeasonAsync(tvShowId, cancellationToken);
        var watchedCounts = await watchedEpisodeRepository.GetWatchedEpisodeCountsBySeasonAsync(
            userId,
            tvShowId,
            cancellationToken);
        var watchedBySeason = watchedCounts.ToDictionary(
            count => count.SeasonNumber,
            count => count.EpisodeCount);

        var seasons = episodeCounts
            .Select(count =>
            {
                var watched = watchedBySeason.GetValueOrDefault(count.SeasonNumber);
                return new SeasonProgressSummaryResult(
                    count.SeasonNumber,
                    count.EpisodeCount,
                    watched,
                    WatchHistoryMapper.CalculateProgressPercentage(watched, count.EpisodeCount));
            })
            .ToList();

        var totalEpisodes = seasons.Sum(season => season.TotalEpisodes);
        var watchedEpisodes = seasons.Sum(season => season.WatchedEpisodes);
        var regularSeasons = seasons.Where(season => season.SeasonNumber >= 1).ToList();
        var regularTotalEpisodes = regularSeasons.Sum(season => season.TotalEpisodes);
        var regularWatchedEpisodes = regularSeasons.Sum(season => season.WatchedEpisodes);
        var isFullyWatched = TvShowCompletionPolicy.IsCaughtUp(regularTotalEpisodes, regularWatchedEpisodes);
        var isCompleted = TvShowCompletionPolicy.IsCompleted(
            TvShowCompletionPolicy.IsConcluded(tvShowStatus),
            regularTotalEpisodes,
            regularWatchedEpisodes);
        var nextEpisode = await episodeRepository.GetFirstUnwatchedForTvShowAsync(tvShowId, userId, cancellationToken);
        progressStopwatch.Stop();
        totalStopwatch.Stop();

        WatchHistoryPerfLogMessages.LogTvShowProgress(
            logger,
            tvShowId,
            totalStopwatch.ElapsedMilliseconds,
            existenceStopwatch.ElapsedMilliseconds,
            progressStopwatch.ElapsedMilliseconds,
            dbRoundTrips: 4);

        return new TvShowWatchProgressResult(
            tvShowId,
            totalEpisodes,
            watchedEpisodes,
            WatchHistoryMapper.CalculateProgressPercentage(watchedEpisodes, totalEpisodes),
            regularTotalEpisodes,
            regularWatchedEpisodes,
            isFullyWatched,
            WatchHistoryMapper.ToNextEpisodeResult(nextEpisode),
            seasons,
            isCompleted);
    }

    public async Task<IReadOnlyList<ContinueWatchingItemResult>> GetContinueWatchingAsync(
        int sectionSize,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        if (sectionSize < 1)
        {
            throw new ValidationException("Section size must be at least 1.");
        }

        var items = await watchedEpisodeRepository.GetContinueWatchingTvShowsAsync(
            userId,
            sectionSize,
            cancellationToken);

        return items
            .Select(item => new ContinueWatchingItemResult(
                item.TvShowId,
                item.Title,
                item.OriginalTitle,
                item.PosterUrl,
                item.BackdropUrl,
                item.FirstAirDate,
                item.VoteAverage,
                item.VoteCount,
                item.LastWatchedAt))
            .ToList();
    }

    public async Task<SeasonWatchProgressResult> GetSeasonWatchProgressAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default)
    {
        if (seasonNumber < 1)
        {
            throw new ValidationException("Season number must be at least 1.");
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);

        var season = await seasonRepository.GetByTvShowIdAndSeasonNumberAsync(tvShowId, seasonNumber, cancellationToken);
        if (season is null)
        {
            throw new NotFoundException($"Season {seasonNumber} for TV show '{tvShowId}' was not found.");
        }

        var totalEpisodes = await episodeRepository.CountByTvShowIdAndSeasonNumberAsync(
            tvShowId,
            seasonNumber,
            cancellationToken);
        var watchedEpisodes = await watchedEpisodeRepository.CountWatchedForSeasonAsync(userId, season.Id, cancellationToken);
        var nextEpisode = await episodeRepository.GetFirstUnwatchedForSeasonAsync(
            tvShowId,
            seasonNumber,
            userId,
            cancellationToken);

        return new SeasonWatchProgressResult(
            tvShowId,
            seasonNumber,
            totalEpisodes,
            watchedEpisodes,
            WatchHistoryMapper.CalculateProgressPercentage(watchedEpisodes, totalEpisodes),
            WatchHistoryMapper.ToSeasonNextEpisodeResult(nextEpisode));
    }

    public async Task<SeasonWatchedEpisodesResult> GetSeasonWatchedEpisodesAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default)
    {
        if (seasonNumber < 1)
        {
            throw new ValidationException("Season number must be at least 1.");
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);

        var season = await seasonRepository.GetByTvShowIdAndSeasonNumberAsync(tvShowId, seasonNumber, cancellationToken);
        if (season is null)
        {
            throw new NotFoundException($"Season {seasonNumber} for TV show '{tvShowId}' was not found.");
        }

        var watchedEpisodeIds = await watchedEpisodeRepository.GetWatchedEpisodeIdsForSeasonAsync(
            userId,
            tvShowId,
            seasonNumber,
            cancellationToken);

        return new SeasonWatchedEpisodesResult(tvShowId, seasonNumber, watchedEpisodeIds);
    }

    public async Task<BulkUpdateEpisodeWatchStateResult> BulkUpdateEpisodeWatchStateAsync(
        Guid tvShowId,
        IReadOnlyList<Guid> episodeIds,
        bool watched,
        CancellationToken cancellationToken = default)
    {
        var validationResult = WatchHistoryBulkValidator.ValidateEpisodeIds(episodeIds);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);

        var distinctIds = episodeIds.Distinct().ToList();
        var validEpisodeIds = await episodeRepository.GetEpisodeIdsBelongingToTvShowAsync(
            tvShowId,
            distinctIds,
            cancellationToken);

        if (validEpisodeIds.Count != distinctIds.Count)
        {
            throw new ValidationException("One or more episode IDs do not belong to the requested TV show.");
        }

        var utcNow = DateTime.UtcNow;
        var affectedCount = watched
            ? await watchedEpisodeRepository.BulkMarkWatchedAsync(userId, validEpisodeIds, utcNow, cancellationToken)
            : await watchedEpisodeRepository.BulkUnmarkWatchedAsync(userId, validEpisodeIds, cancellationToken);
        await InvalidateProfileStatisticsAsync(userId, cancellationToken);

        return new BulkUpdateEpisodeWatchStateResult(affectedCount, watched ? utcNow : null);
    }

    public async Task<MarkThroughEpisodeResult> MarkThroughEpisodeAsync(
        Guid tvShowId,
        Guid episodeId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);
        await EnsureEpisodeExistsAsync(episodeId, cancellationToken);

        var episodeIds = await episodeRepository.GetEpisodeIdsForTvShowUpToEpisodeAsync(
            tvShowId,
            episodeId,
            cancellationToken);

        if (episodeIds.Count == 0)
        {
            throw new NotFoundException("The requested episode was not found for this TV show.");
        }

        var utcNow = DateTime.UtcNow;
        var affectedCount = await watchedEpisodeRepository.BulkMarkWatchedAsync(
            userId,
            episodeIds,
            utcNow,
            cancellationToken);
        await InvalidateProfileStatisticsAsync(userId, cancellationToken);

        return new MarkThroughEpisodeResult(episodeId, affectedCount, utcNow);
    }

    public async Task<BulkUpdateEpisodeWatchStateResult> BulkUpdateSeasonWatchStateAsync(
        Guid tvShowId,
        int seasonNumber,
        bool watched,
        CancellationToken cancellationToken = default)
    {
        if (seasonNumber < 1)
        {
            throw new ValidationException("Season number must be at least 1.");
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);

        var season = await seasonRepository.GetByTvShowIdAndSeasonNumberAsync(tvShowId, seasonNumber, cancellationToken);
        if (season is null)
        {
            throw new NotFoundException($"Season {seasonNumber} for TV show '{tvShowId}' was not found.");
        }

        await getSeasonService.GetSeasonAsync(tvShowId, seasonNumber, cancellationToken);

        var episodeIds = await episodeRepository.GetEpisodeIdsForSeasonAsync(tvShowId, seasonNumber, cancellationToken);
        if (episodeIds.Count == 0)
        {
            return new BulkUpdateEpisodeWatchStateResult(0, null);
        }

        var utcNow = DateTime.UtcNow;
        var affectedCount = watched
            ? await watchedEpisodeRepository.BulkMarkWatchedAsync(userId, episodeIds, utcNow, cancellationToken)
            : await watchedEpisodeRepository.BulkUnmarkWatchedAsync(userId, episodeIds, cancellationToken);
        await InvalidateProfileStatisticsAsync(userId, cancellationToken);

        return new BulkUpdateEpisodeWatchStateResult(affectedCount, watched ? utcNow : null);
    }

    public async Task<BulkUpdateEpisodeWatchStateResult> BulkUpdateTvShowWatchStateAsync(
        Guid tvShowId,
        bool watched,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var ensureTvShowStopwatch = Stopwatch.StartNew();
        await EnsureTvShowExistsAsync(tvShowId, cancellationToken);
        ensureTvShowStopwatch.Stop();

        var ingestionCheckStopwatch = Stopwatch.StartNew();
        var ingestionCheck = await seasonRepository.CheckRegularEpisodeIngestionRequiredAsync(tvShowId, cancellationToken);
        ingestionCheckStopwatch.Stop();

        var ensureIngestedMs = 0L;
        if (ingestionCheck.IsRequired)
        {
            var ensureIngestedStopwatch = Stopwatch.StartNew();
            await EnsureRegularSeasonEpisodesIngestedAsync(tvShowId, cancellationToken);
            ensureIngestedStopwatch.Stop();
            ensureIngestedMs = ensureIngestedStopwatch.ElapsedMilliseconds;
        }

        var episodeIdsStopwatch = Stopwatch.StartNew();
        var episodeIds = await episodeRepository.GetEpisodeIdsForRegularSeasonsAsync(tvShowId, cancellationToken);
        episodeIdsStopwatch.Stop();

        if (episodeIds.Count == 0)
        {
            totalStopwatch.Stop();
            LogTvWatchStatePerf(
                tvShowId,
                watched,
                totalStopwatch.ElapsedMilliseconds,
                ensureTvShowStopwatch.ElapsedMilliseconds,
                ingestionCheckStopwatch.ElapsedMilliseconds,
                ingestionCheck,
                ensureIngestedMs,
                episodeIdsStopwatch.ElapsedMilliseconds,
                episodeCount: 0,
                bulkWriteMs: 0,
                affectedCount: 0,
                analyticsDispatchMs: 0);

            return new BulkUpdateEpisodeWatchStateResult(0, null);
        }

        var utcNow = DateTime.UtcNow;
        var bulkWriteStopwatch = Stopwatch.StartNew();
        var affectedCount = watched
            ? await watchedEpisodeRepository.BulkMarkWatchedAsync(userId, episodeIds, utcNow, cancellationToken)
            : await watchedEpisodeRepository.BulkUnmarkWatchedAsync(userId, episodeIds, cancellationToken);
        bulkWriteStopwatch.Stop();

        var analyticsStopwatch = Stopwatch.StartNew();
        await InvalidateProfileStatisticsAsync(userId, cancellationToken);
        analyticsStopwatch.Stop();

        totalStopwatch.Stop();
        LogTvWatchStatePerf(
            tvShowId,
            watched,
            totalStopwatch.ElapsedMilliseconds,
            ensureTvShowStopwatch.ElapsedMilliseconds,
            ingestionCheckStopwatch.ElapsedMilliseconds,
            ingestionCheck,
            ensureIngestedMs,
            episodeIdsStopwatch.ElapsedMilliseconds,
            episodeIds.Count,
            bulkWriteStopwatch.ElapsedMilliseconds,
            affectedCount,
            analyticsStopwatch.ElapsedMilliseconds);

        return new BulkUpdateEpisodeWatchStateResult(affectedCount, watched ? utcNow : null);
    }

    private void LogTvWatchStatePerf(
        Guid tvShowId,
        bool watched,
        long totalMs,
        long ensureTvShowExistsMs,
        long ingestionRequiredCheckMs,
        RegularEpisodeIngestionCheckResult ingestionCheck,
        long ensureIngestedMs,
        long episodeIdsLoadMs,
        int episodeCount,
        long bulkWriteMs,
        int affectedCount,
        long analyticsDispatchMs)
    {
        var missingSeasonNumbers = ingestionCheck.MissingSeasonNumbers.Count == 0
            ? "-"
            : string.Join(',', ingestionCheck.MissingSeasonNumbers.Take(12));

        WatchHistoryPerfLogMessages.LogTvWatchState(
            logger,
            tvShowId,
            correlationId: null,
            watched,
            totalMs,
            ensureTvShowExistsMs,
            ingestionRequiredCheckMs,
            ingestionCheck.CatalogMetadataQueryMs,
            ingestionCheck.SeasonsWithEpisodesQueryMs,
            ingestionCheck.IsRequired,
            ingestionCheck.RegularSeasonCount,
            ingestionCheck.SeasonsWithEpisodeRowsCount,
            ingestionCheck.SeasonsMissingEpisodesCount,
            missingSeasonNumbers,
            ensureIngestedMs,
            episodeIdsLoadMs,
            episodeCount,
            bulkWriteMs,
            affectedCount,
            analyticsDispatchMs);
    }

    private async Task EnsureRegularSeasonEpisodesIngestedAsync(
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();

        var summaryStopwatch = Stopwatch.StartNew();
        var hydrationResult = await seasonSummaryHydrator.EnsureSeasonSummariesAsync(tvShowId, cancellationToken);
        summaryStopwatch.Stop();
        if (hydrationResult.ProviderCatalogRefreshed)
        {
            await catalogSyncStateService.MarkRefreshedAsync(
                tvShowId,
                TvShowCatalogRefreshReason.DetailHydration,
                DateTime.UtcNow,
                cancellationToken);
        }

        var seasonLookupStopwatch = Stopwatch.StartNew();
        var seasonsWithEpisodes = await seasonRepository.GetRegularSeasonNumbersWithEpisodesAsync(
            tvShowId,
            cancellationToken);
        var seasonsNeedingHydration = hydrationResult.TvShow.Seasons
            .Where(season => season.SeasonNumber >= 1)
            .Where(season => !seasonsWithEpisodes.Contains(season.SeasonNumber))
            .Where(season => season.EpisodeCount != 0)
            .Select(season => season.SeasonNumber)
            .OrderBy(seasonNumber => seasonNumber)
            .ToList();
        seasonLookupStopwatch.Stop();

        var seasonsHydrated = 0;
        foreach (var seasonNumber in seasonsNeedingHydration)
        {
            await getSeasonService.GetSeasonAsync(tvShowId, seasonNumber, cancellationToken);
            seasonsHydrated++;
        }

        totalStopwatch.Stop();
        WatchHistoryPerfLogMessages.LogTvShowHydration(
            logger,
            tvShowId,
            totalStopwatch.ElapsedMilliseconds,
            summaryStopwatch.ElapsedMilliseconds,
            seasonLookupStopwatch.ElapsedMilliseconds,
            seasonsNeedingHydration.Count,
            seasonsHydrated);
    }

    private Task InvalidateProfileStatisticsAsync(Guid userId, CancellationToken cancellationToken) =>
        BackgroundAnalyticsInvalidation.RunAsync(
            analyticsCacheInvalidator,
            analyticsScopeFactory,
            userId,
            cancellationToken);

    private static void ValidatePagination(int page, int pageSize)
    {
        var validationResult = SearchPaginationValidator.Validate(page, pageSize);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ErrorMessage!);
        }
    }

    private static int GetRecentHistoryFetchCount(int page, int pageSize)
    {
        var fetchCount = (long)page * pageSize;
        if (fetchCount > MaxRecentHistoryDepth)
        {
            throw new ValidationException(
                $"Recent watch history can only page through the latest {MaxRecentHistoryDepth} items.");
        }

        return (int)fetchCount;
    }

    private async Task EnsureMovieExistsAsync(Guid movieId, CancellationToken cancellationToken)
    {
        if (!await movieRepository.ExistsAsync(movieId, cancellationToken))
        {
            throw new NotFoundException("The requested movie was not found.");
        }
    }

    private async Task EnsureEpisodeExistsAsync(Guid episodeId, CancellationToken cancellationToken)
    {
        if (!await episodeRepository.ExistsAsync(episodeId, cancellationToken))
        {
            throw new NotFoundException("The requested episode was not found.");
        }
    }

    private async Task EnsureTvShowExistsAsync(Guid tvShowId, CancellationToken cancellationToken)
    {
        if (!await tvShowRepository.ExistsAsync(tvShowId, cancellationToken))
        {
            throw new NotFoundException("The requested TV show was not found.");
        }
    }
}
