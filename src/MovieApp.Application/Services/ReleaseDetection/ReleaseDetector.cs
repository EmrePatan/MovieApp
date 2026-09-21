using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.ReleaseDetection;
using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ReleaseDetection;

public sealed class ReleaseDetector(
    IReleaseDetectionCatalogRepository catalogRepository,
    ICatalogReleaseEventRepository catalogReleaseEventRepository) : IReleaseDetector
{
    public async Task<ReleaseDetectionResult> ScanTvShowAsync(
        Guid tvShowId,
        ReleaseDetectionMode mode,
        DateOnly boundary,
        IReadOnlyList<Season>? seasons = null,
        CancellationToken cancellationToken = default)
    {
        var source = MapModeToSource(mode);
        var detectedAtUtc = DateTime.UtcNow;

        seasons ??= await catalogRepository.GetSeasonsWithEpisodesAsync(tvShowId, cancellationToken);
        var existingDedupeKeys = await catalogReleaseEventRepository.GetDedupeKeysForTvShowAsync(
            tvShowId,
            cancellationToken);

        var candidateEvents = ReleaseDetectionScanner.DetectMissingEvents(
            tvShowId,
            seasons,
            boundary,
            new HashSet<string>(StringComparer.Ordinal),
            source,
            detectedAtUtc);

        var missingEvents = candidateEvents
            .Where(releaseEvent => !existingDedupeKeys.Contains(releaseEvent.DedupeKey))
            .ToList();

        var eventsAlreadyExisted = candidateEvents.Count - missingEvents.Count;

        var insertResult = await catalogReleaseEventRepository.TryAddEventsAsync(
            missingEvents,
            cancellationToken);

        eventsAlreadyExisted += insertResult.EventsAlreadyExisted;

        var createdEpisodeEvents = insertResult.CreatedEventIds.Count == 0
            ? 0
            : missingEvents.Count(releaseEvent =>
                insertResult.CreatedEventIds.Contains(releaseEvent.Id) &&
                releaseEvent.EventType == CatalogReleaseEventType.NewEpisode);

        var createdSeasonPremiereEvents = insertResult.CreatedEventIds.Count == 0
            ? 0
            : missingEvents.Count(releaseEvent =>
                insertResult.CreatedEventIds.Contains(releaseEvent.Id) &&
                releaseEvent.EventType == CatalogReleaseEventType.NewSeasonPremiere);

        return new ReleaseDetectionResult(
            insertResult.EventsCreated,
            eventsAlreadyExisted,
            createdEpisodeEvents,
            createdSeasonPremiereEvents,
            insertResult.CreatedEventIds);
    }

    private static CatalogReleaseEventSource MapModeToSource(ReleaseDetectionMode mode) =>
        mode switch
        {
            ReleaseDetectionMode.BaselineAbsorb => CatalogReleaseEventSource.BaselineAbsorb,
            ReleaseDetectionMode.BoundaryCheck => CatalogReleaseEventSource.BoundaryDetection,
            ReleaseDetectionMode.PostRefresh => CatalogReleaseEventSource.ProviderRefresh,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported release detection mode.")
        };
}
