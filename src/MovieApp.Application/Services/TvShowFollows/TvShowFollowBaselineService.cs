using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.ReleaseDetection;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.TvShowFollows;

public sealed class TvShowFollowBaselineService(
    ITvShowFollowRepository tvShowFollowRepository,
    ITvShowRepository tvShowRepository,
    ITvShowSeasonSummaryHydrator seasonSummaryHydrator,
    IReleaseDetectionCatalogRepository releaseDetectionCatalogRepository,
    IServiceScopeFactory scopeFactory,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    ITvShowCatalogSyncStateService catalogSyncStateService,
    IReleaseDetector releaseDetector,
    ILogger<TvShowFollowBaselineService> logger) : ITvShowFollowBaselineService
{
    public async Task EstablishAsync(CatalogFollow follow, CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var summaryHydrationMs = 0L;
        var providerSeasonFetchMs = 0L;
        var seasonPersistenceMs = 0L;
        var catalogReloadMs = 0L;
        var markRefreshedMs = 0L;
        var releaseScanMs = 0L;
        var baselineCompletionMs = 0L;
        var seasonsHydrated = 0;
        var episodesHydrated = 0;

        var currentFollow = await tvShowFollowRepository.GetForUserAndTvShowForUpdateAsync(
            follow.UserId,
            follow.TvShowId,
            cancellationToken);

        if (currentFollow is null)
        {
            throw new NotFoundException("The requested TV show follow was not found.");
        }

        follow = currentFollow;

        if (follow.IsBaselineEstablished)
        {
            return;
        }

        if (!follow.NotifyFromUtc.HasValue)
        {
            throw new InvalidOperationException("NotifyFromUtc must be set before baseline establishment.");
        }

        var boundaryUtc = follow.NotifyFromUtc.Value;
        var boundaryDate = DateOnly.FromDateTime(boundaryUtc);
        var tvShowId = follow.TvShowId;

        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            throw new NotFoundException("The requested TV show was not found.");
        }

        var providerCatalogRefreshed = false;

        if (TvShowFollowBaselineCatalogRules.NeedsSeasonSummaries(tvShow.Seasons))
        {
            var summaryStopwatch = Stopwatch.StartNew();
            var hydrationResult = await seasonSummaryHydrator.EnsureSeasonSummariesAsync(
                tvShowId,
                cancellationToken);
            summaryStopwatch.Stop();
            summaryHydrationMs = summaryStopwatch.ElapsedMilliseconds;

            if (hydrationResult.ProviderCatalogRefreshed)
            {
                providerCatalogRefreshed = true;
            }
        }

        var seasons = await releaseDetectionCatalogRepository.GetSeasonsWithEpisodesAsync(
            tvShowId,
            cancellationToken);

        var seasonsToHydrate = DetermineSeasonsToHydrate(seasons, boundaryDate);
        if (seasonsToHydrate.Count > 0)
        {
            var externalId = ResolveExternalId(tvShow);

            var hydrationResult = await HydrateSeasonsAsync(
                tvShowId,
                externalId,
                seasonsToHydrate,
                cancellationToken);
            providerSeasonFetchMs = hydrationResult.ProviderSeasonFetchMs;
            seasonPersistenceMs = hydrationResult.SeasonPersistenceMs;
            seasonsHydrated = hydrationResult.SeasonsHydrated;
            episodesHydrated = hydrationResult.EpisodesHydrated;
            providerCatalogRefreshed = true;

            var catalogReloadStopwatch = Stopwatch.StartNew();
            seasons = await releaseDetectionCatalogRepository.GetSeasonsWithEpisodesAsync(
                tvShowId,
                cancellationToken);
            catalogReloadStopwatch.Stop();
            catalogReloadMs = catalogReloadStopwatch.ElapsedMilliseconds;
        }

        if (providerCatalogRefreshed)
        {
            var markRefreshedStopwatch = Stopwatch.StartNew();
            await catalogSyncStateService.MarkRefreshedAsync(
                tvShowId,
                TvShowCatalogRefreshReason.FollowBaseline,
                DateTime.UtcNow,
                cancellationToken);
            markRefreshedStopwatch.Stop();
            markRefreshedMs = markRefreshedStopwatch.ElapsedMilliseconds;
        }

        var releaseScanStopwatch = Stopwatch.StartNew();
        await releaseDetector.ScanTvShowAsync(
            tvShowId,
            ReleaseDetectionMode.BaselineAbsorb,
            boundaryDate,
            seasons,
            cancellationToken);
        releaseScanStopwatch.Stop();
        releaseScanMs = releaseScanStopwatch.ElapsedMilliseconds;

        var baselineCompletionStopwatch = Stopwatch.StartNew();
        follow.EstablishBaseline(DateTime.UtcNow);
        await tvShowFollowRepository.SaveChangesAsync(cancellationToken);
        baselineCompletionStopwatch.Stop();
        baselineCompletionMs = baselineCompletionStopwatch.ElapsedMilliseconds;

        totalStopwatch.Stop();
        TvShowFollowBaselineLogMessages.LogBaselineCompleted(
            logger,
            tvShowId,
            totalStopwatch.ElapsedMilliseconds,
            summaryHydrationMs,
            providerSeasonFetchMs,
            seasonPersistenceMs,
            catalogReloadMs,
            markRefreshedMs,
            releaseScanMs,
            baselineCompletionMs,
            seasonsHydrated,
            episodesHydrated);
    }

    private static List<int> DetermineSeasonsToHydrate(
        IReadOnlyList<Season> seasons,
        DateOnly boundaryDate)
    {
        var seasonsToHydrate = new List<int>();

        foreach (var season in seasons.Where(season => season.SeasonNumber >= 1))
        {
            var regularEpisodes = season.Episodes
                .Where(episode => episode.EpisodeNumber >= 1)
                .ToList();

            if (TvShowFollowBaselineCatalogRules.NeedsEpisodeHydration(
                    season,
                    regularEpisodes.Count,
                    boundaryDate,
                    regularEpisodes))
            {
                seasonsToHydrate.Add(season.SeasonNumber);
            }
        }

        return seasonsToHydrate;
    }

    private string ResolveExternalId(TvShow tvShow)
    {
        var externalId = externalIdResolver.Resolve(tvShow.TmdbId, tvShow.TvdbId, tvShow.ImdbId);
        if (externalId is null)
        {
            throw new TvShowFollowBaselineException(
                "TV show provider identity is unavailable for required baseline hydration.");
        }

        return externalId;
    }

    private async Task<SeasonHydrationResult> HydrateSeasonsAsync(
        Guid tvShowId,
        string externalId,
        List<int> seasonNumbers,
        CancellationToken cancellationToken)
    {
        var providerSeasonFetchMs = 0L;
        var seasonPersistenceMs = 0L;
        var seasonsHydrated = 0;
        var episodesHydrated = 0;
        var batchSize = TvShowFollowBaselineHydrationOptions.MaxSeasonsPerPersistenceBatch;

        for (var index = 0; index < seasonNumbers.Count; index += batchSize)
        {
            var batch = seasonNumbers.Skip(index).Take(batchSize).ToArray();

            var providerFetchStopwatch = Stopwatch.StartNew();
            var hydrationTasks = batch
                .Select(seasonNumber => FetchSeasonAsync(externalId, seasonNumber, cancellationToken))
                .ToArray();
            var batchResults = await Task.WhenAll(hydrationTasks);
            providerFetchStopwatch.Stop();
            providerSeasonFetchMs += providerFetchStopwatch.ElapsedMilliseconds;

            var persistenceStopwatch = Stopwatch.StartNew();
            using (var scope = scopeFactory.CreateScope())
            {
                await scope.ServiceProvider
                    .GetRequiredService<ISeasonRepository>()
                    .UpsertSeasonsFromProviderAsync(tvShowId, batchResults, cancellationToken);
            }

            persistenceStopwatch.Stop();
            seasonPersistenceMs += persistenceStopwatch.ElapsedMilliseconds;
            seasonsHydrated += batchResults.Length;
            episodesHydrated += batchResults.Sum(season => season.Episodes.Count);
        }

        return new SeasonHydrationResult(
            providerSeasonFetchMs,
            seasonPersistenceMs,
            seasonsHydrated,
            episodesHydrated);
    }

    private async Task<SeasonProviderDetails> FetchSeasonAsync(
        string externalId,
        int seasonNumber,
        CancellationToken cancellationToken)
    {
        var providerSeason = await tvShowDataProvider.GetSeasonAsync(
            externalId,
            seasonNumber,
            cancellationToken);

        if (providerSeason is null)
        {
            throw new TvShowFollowBaselineException(
                $"Unable to hydrate season {seasonNumber} required for follow baseline.");
        }

        return providerSeason;
    }

    private sealed record SeasonHydrationResult(
        long ProviderSeasonFetchMs,
        long SeasonPersistenceMs,
        int SeasonsHydrated,
        int EpisodesHydrated);
}
