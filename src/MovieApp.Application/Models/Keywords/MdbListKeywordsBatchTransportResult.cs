using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.Keywords;

public sealed record MdbListKeywordsBatchTransportResult(
    CatalogContentType MediaType,
    IReadOnlyDictionary<int, IReadOnlyList<MdbListKeywordTransportItem>> KeywordsByTmdbId,
    MdbListKeywordTransportTelemetry Telemetry);

public sealed record MdbListKeywordTransportTelemetry(
    int StatusCode,
    long LatencyMilliseconds,
    int? RateLimitRemaining,
    DateTimeOffset? RateLimitResetUtc);
