using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.ReleaseDetection;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.Application.Services.TvShowChanges;

public sealed class TvShowChangesTargetedRefreshService(
    ITvShowRepository tvShowRepository,
    ICatalogProviderUpsertService catalogProviderUpsertService,
    ISeasonRepository seasonRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    IReleaseDetectionCatalogRepository releaseDetectionCatalogRepository,
    IReleaseDetector releaseDetector,
    ITvShowCatalogSyncStateService catalogSyncStateService,
    ITvShowCatalogDetailsCacheInvalidator cacheInvalidator) : ITvShowChangesTargetedRefreshService
{
    public async Task<TmdbChangesTargetRefreshResult> RefreshRelevantShowAsync(
        Guid tvShowId,
        DateOnly boundaryDate,
        DateOnly changeSignalDate,
        CancellationToken cancellationToken = default)
    {
        var tvShow = await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken);
        if (tvShow is null)
        {
            return TmdbChangesTargetRefreshResult.SkippedNotFound();
        }

        var externalId = externalIdResolver.Resolve(tvShow.TmdbId, tvShow.TvdbId, tvShow.ImdbId);
        if (externalId is null)
        {
            return TmdbChangesTargetRefreshResult.SkippedUnavailable();
        }

        var providerDetails = await tvShowDataProvider.GetTvShowAsync(externalId, cancellationToken);
        if (providerDetails is null)
        {
            return TmdbChangesTargetRefreshResult.SkippedUnavailable();
        }

        await catalogProviderUpsertService.UpsertTvShowFromProviderAsync(
            providerDetails,
            enrichKeywords: true,
            cancellationToken);

        var seasons = await releaseDetectionCatalogRepository.GetSeasonsWithEpisodesAsync(
            tvShowId,
            cancellationToken);

        var seasonsToHydrate = TvShowChangesRefreshRules.DetermineSeasonsToHydrate(seasons, boundaryDate);
        var hydratedSeasons = new List<TmdbChangesHydratedSeasonCacheTarget>();

        foreach (var seasonNumber in seasonsToHydrate)
        {
            var providerSeason = await tvShowDataProvider.GetSeasonAsync(
                externalId,
                seasonNumber,
                cancellationToken);

            if (providerSeason is null)
            {
                return TmdbChangesTargetRefreshResult.SkippedUnavailable();
            }

            var hydratedSeason = await seasonRepository.UpsertFromProviderAsync(
                tvShowId,
                providerSeason,
                cancellationToken);

            hydratedSeasons.Add(new TmdbChangesHydratedSeasonCacheTarget(
                seasonNumber,
                hydratedSeason.Episodes
                    .Where(episode => episode.EpisodeNumber >= 1)
                    .Select(episode => episode.EpisodeNumber)
                    .ToList()));
        }

        await releaseDetector.ScanTvShowAsync(
            tvShowId,
            ReleaseDetectionMode.PostRefresh,
            boundaryDate,
            cancellationToken);

        await catalogSyncStateService.MarkChangesSyncAsync(
            tvShowId,
            DateTime.UtcNow,
            changeSignalDate,
            cancellationToken);

        await cacheInvalidator.InvalidateAsync(tvShowId, hydratedSeasons, cancellationToken);

        return TmdbChangesTargetRefreshResult.Refreshed(hydratedSeasons);
    }
}
