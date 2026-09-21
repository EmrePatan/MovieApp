using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Abstractions.ReleaseDetection;
using MovieApp.Application.Abstractions.TvShows;
using MovieApp.Application.Models.HotRelease;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Application.Services.TvShowChanges;

namespace MovieApp.Application.Services.HotRelease;

public sealed class HotReleaseCandidateProcessor(
    IReleaseDetectionCatalogRepository catalogRepository,
    ISeasonRepository seasonRepository,
    ITvShowDataProvider tvShowDataProvider,
    ITvShowExternalIdResolver externalIdResolver,
    IReleaseDetector releaseDetector,
    ITvShowCatalogSyncStateService catalogSyncStateService) : IHotReleaseCandidateProcessor
{
    public async Task<HotReleaseCandidateProcessResult> ProcessAsync(
        HotReleaseCandidate candidate,
        DateOnly boundaryDate,
        CancellationToken cancellationToken = default)
    {
        var seasons = await catalogRepository.GetSeasonsWithEpisodesAsync(
            candidate.TvShowId,
            cancellationToken);

        var seasonsToHydrate = TvShowChangesRefreshRules.DetermineSeasonsToHydrate(seasons, boundaryDate);
        var hydrated = false;

        if (seasonsToHydrate.Count > 0)
        {
            var externalId = externalIdResolver.Resolve(
                candidate.TmdbId,
                candidate.TvdbId,
                candidate.ImdbId);

            if (externalId is null)
            {
                throw new InvalidOperationException(
                    $"TV show '{candidate.TvShowId}' does not have a resolvable provider identity.");
            }

            foreach (var seasonNumber in seasonsToHydrate)
            {
                var providerSeason = await tvShowDataProvider.GetSeasonAsync(
                    externalId,
                    seasonNumber,
                    cancellationToken);

                if (providerSeason is null)
                {
                    throw new InvalidOperationException(
                        $"Provider season {seasonNumber} was unavailable for TV show '{candidate.TvShowId}'.");
                }

                await seasonRepository.UpsertFromProviderAsync(
                    candidate.TvShowId,
                    providerSeason,
                    cancellationToken);
            }

            hydrated = true;
            seasons = await catalogRepository.GetSeasonsWithEpisodesAsync(
                candidate.TvShowId,
                cancellationToken);
        }

        var detectionResult = await releaseDetector.ScanTvShowAsync(
            candidate.TvShowId,
            ReleaseDetectionMode.BoundaryCheck,
            boundaryDate,
            cancellationToken: cancellationToken);

        var nextHotCheckAtUtc = HotReleaseNextCheckCalculator.ComputeNextHotCheckAtUtc(
            seasons,
            boundaryDate);
        var updatedAtUtc = DateTime.UtcNow;

        if (hydrated)
        {
            await catalogSyncStateService.MarkHotReleaseAsync(
                candidate.TvShowId,
                updatedAtUtc,
                nextHotCheckAtUtc,
                cancellationToken);
        }
        else
        {
            await catalogSyncStateService.UpdateNextHotCheckAsync(
                candidate.TvShowId,
                nextHotCheckAtUtc,
                updatedAtUtc,
                cancellationToken);
        }

        return new HotReleaseCandidateProcessResult(hydrated, detectionResult.EventsCreated);
    }
}
