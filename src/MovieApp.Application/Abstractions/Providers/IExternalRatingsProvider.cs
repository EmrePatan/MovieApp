using MovieApp.Application.Models.ExternalRatings;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Abstractions.Providers;

public sealed record ExternalRatingsProviderFetchResult(
    ExternalRatingSnapshotPayload Payload,
    bool IsNotFound,
    ExternalRatingsProviderTelemetry Telemetry);

public sealed record ExternalRatingsProviderTelemetry(
    int StatusCode,
    long LatencyMilliseconds,
    int? RateLimitRemaining,
    DateTimeOffset? RateLimitResetUtc);

public interface IExternalRatingsProvider
{
    Task<ExternalRatingsProviderFetchResult?> FetchAsync(
        CatalogContentType mediaType,
        int tmdbId,
        CancellationToken cancellationToken = default);
}
