using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.ExternalRatings;
using MovieApp.Application.Services.ExternalRatings;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Providers.MdbList;

public sealed class MdbListExternalRatingsProvider(MdbListApiClient apiClient) : IExternalRatingsProvider
{
    public async Task<ExternalRatingsProviderFetchResult?> FetchAsync(
        CatalogContentType mediaType,
        int tmdbId,
        CancellationToken cancellationToken = default)
    {
        var mediaSegment = mediaType == CatalogContentType.Movie ? "movie" : "show";
        var response = await apiClient.GetByTmdbIdAsync(mediaSegment, tmdbId, cancellationToken);
        if (response is null)
        {
            return null;
        }

        var telemetry = new ExternalRatingsProviderTelemetry(
            response.Telemetry.StatusCode,
            response.Telemetry.LatencyMilliseconds,
            response.Telemetry.RateLimitRemaining,
            response.Telemetry.RateLimitResetUtc);

        if (response.IsNotFound)
        {
            return new ExternalRatingsProviderFetchResult(
                new ExternalRatingSnapshotPayload { IsNegative = true },
                true,
                telemetry);
        }

        var ratings = new List<ExternalRatingItem>();
        foreach (var raw in response.Payload?.Ratings ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw.Source) || raw.Value is null)
            {
                continue;
            }

            if (string.Equals(raw.Source, "tmdb", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalized = ExternalRatingSourceNormalizer.TryNormalize(
                raw.Source,
                raw.Value.Value,
                raw.Votes);

            if (normalized is not null)
            {
                ratings.Add(normalized);
            }
        }

        return new ExternalRatingsProviderFetchResult(
            new ExternalRatingSnapshotPayload { Ratings = ratings },
            false,
            telemetry);
    }
}
