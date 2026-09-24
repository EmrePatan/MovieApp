using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.ExternalRatings;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.ExternalRatings;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ExternalRatings;

public sealed class ExternalRatingsRefreshService(
    IExternalRatingsProvider externalRatingsProvider,
    IExternalRatingSnapshotRepository snapshotRepository,
    IExternalRatingsRefreshCoalescer refreshCoalescer,
    IExternalRatingsFeatureState featureState,
    TimeProvider timeProvider,
    ILogger<ExternalRatingsRefreshService> logger) : IExternalRatingsRefreshService
{
    private const ExternalRatingsProvider Provider = ExternalRatingsProvider.MdbList;

    public Task RefreshAsync(
        CatalogContentType mediaType,
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        if (!featureState.IsOperational)
        {
            return Task.CompletedTask;
        }

        var coalesceKey = ExternalRatingsRefreshKeys.Build(mediaType, tmdbId);
        return refreshCoalescer.CoalesceAsync(
            coalesceKey,
            () => RefreshInternalAsync(mediaType, tmdbId, cancellationToken));
    }

    private async Task RefreshInternalAsync(
        CatalogContentType mediaType,
        int tmdbId,
        CancellationToken cancellationToken)
    {
        var fetchResult = await externalRatingsProvider.FetchAsync(mediaType, tmdbId, cancellationToken);
        if (fetchResult is null)
        {
            return;
        }

        LogTelemetry(mediaType, tmdbId, fetchResult);

        if (fetchResult.IsNotFound)
        {
            await PersistSnapshotAsync(
                mediaType,
                tmdbId,
                new ExternalRatingSnapshotPayload { IsNegative = true },
                cancellationToken);
            return;
        }

        await PersistSnapshotAsync(mediaType, tmdbId, fetchResult.Payload, cancellationToken);
    }

    private async Task PersistSnapshotAsync(
        CatalogContentType mediaType,
        int tmdbId,
        ExternalRatingSnapshotPayload payload,
        CancellationToken cancellationToken)
    {
        await snapshotRepository.UpsertAsync(
            mediaType,
            tmdbId,
            Provider,
            payload,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
    }

    private void LogTelemetry(CatalogContentType mediaType, int tmdbId, ExternalRatingsProviderFetchResult fetchResult)
    {
        var telemetry = fetchResult.Telemetry;
        var outcome = fetchResult.IsNotFound ? "negative" : "refreshed";
        var mediaLabel = mediaType == CatalogContentType.Movie ? "Movie" : "Tv";

        ExternalRatingsLogMessages.LogProviderFetch(
            logger,
            outcome,
            mediaLabel,
            tmdbId,
            telemetry.StatusCode,
            telemetry.LatencyMilliseconds,
            telemetry.RateLimitRemaining);

        if (telemetry.StatusCode == 429
            || (telemetry.RateLimitRemaining is int remaining && remaining <= 50))
        {
            ExternalRatingsLogMessages.LogRateLimitWarning(
                logger,
                mediaLabel,
                tmdbId,
                telemetry.StatusCode,
                telemetry.RateLimitRemaining);
        }
    }
}
