using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.ExternalRatings;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.ExternalRatings;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ExternalRatings;

public sealed class ExternalRatingsAccessService(
    IExternalRatingSnapshotRepository snapshotRepository,
    IExternalRatingsRefreshService refreshService,
    IExternalRatingsRefreshJobEnqueuer refreshJobEnqueuer,
    IExternalRatingsFeatureState featureState,
    IOptions<ExternalRatingsOptions> options,
    TimeProvider timeProvider)
{
    private const ExternalRatingsProvider Provider = ExternalRatingsProvider.MdbList;

    private static readonly ExternalRatingsResult Empty = new(null, false, []);

    public async Task<ExternalRatingsResult> GetAsync(
        CatalogContentType mediaType,
        int? tmdbId,
        CancellationToken cancellationToken)
    {
        if (!featureState.IsOperational || tmdbId is null)
        {
            return Empty;
        }

        var snapshot = await snapshotRepository.GetAsync(mediaType, tmdbId.Value, Provider, cancellationToken);
        var payload = snapshot is null
            ? null
            : ExternalRatingSnapshotSerializer.Deserialize(snapshot.PayloadJson);

        var policy = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var freshWindow = TimeSpan.FromHours(Math.Max(1, policy.FreshHours));
        var staleWindow = TimeSpan.FromDays(Math.Max(1, policy.StaleDays));
        var negativeWindow = TimeSpan.FromHours(Math.Max(1, policy.NegativeHours));

        var cacheState = ExternalRatingsCachePolicy.Evaluate(
            snapshot?.FetchedAtUtc,
            payload?.IsNegative ?? false,
            freshWindow,
            staleWindow,
            negativeWindow,
            now);

        switch (cacheState)
        {
            case ExternalRatingsCacheState.NegativeFresh:
                return Empty;

            case ExternalRatingsCacheState.Fresh:
                return ToResult(snapshot!, payload!, isStale: false);

            case ExternalRatingsCacheState.StaleUsable:
                refreshJobEnqueuer.EnqueueRefresh(mediaType, tmdbId.Value);
                return ToResult(snapshot!, payload!, isStale: true);

            case ExternalRatingsCacheState.Miss:
            case ExternalRatingsCacheState.Expired:
                return await HandleMissOrExpiredAsync(
                    mediaType,
                    tmdbId.Value,
                    snapshot,
                    payload,
                    cancellationToken);
        }

        return Empty;
    }

    private async Task<ExternalRatingsResult> HandleMissOrExpiredAsync(
        CatalogContentType mediaType,
        int tmdbId,
        ExternalRatingSnapshot? previousSnapshot,
        ExternalRatingSnapshotPayload? previousPayload,
        CancellationToken cancellationToken)
    {
        try
        {
            await refreshService.RefreshAsync(mediaType, tmdbId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            if (previousSnapshot is not null && previousPayload is not null && !previousPayload.IsNegative)
            {
                return ToResult(previousSnapshot, previousPayload, isStale: true);
            }

            return Empty;
        }

        var refreshed = await snapshotRepository.GetAsync(mediaType, tmdbId, Provider, cancellationToken);
        if (refreshed is null)
        {
            if (previousSnapshot is not null && previousPayload is not null && !previousPayload.IsNegative)
            {
                return ToResult(previousSnapshot, previousPayload, isStale: true);
            }

            return Empty;
        }

        var refreshedPayload = ExternalRatingSnapshotSerializer.Deserialize(refreshed.PayloadJson);
        if (refreshedPayload.IsNegative)
        {
            return Empty;
        }

        return ToResult(refreshed, refreshedPayload, isStale: false);
    }

    private static ExternalRatingsResult ToResult(
        ExternalRatingSnapshot snapshot,
        ExternalRatingSnapshotPayload payload,
        bool isStale) =>
        new(snapshot.FetchedAtUtc, isStale, payload.Ratings);
}
