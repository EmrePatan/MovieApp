using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.ExternalRatings;

internal static partial class ExternalRatingsLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "External ratings cache {Outcome} for {MediaType} tmdb:{TmdbId} status:{StatusCode} latencyMs:{LatencyMs} rateRemaining:{RateRemaining}")]
    public static partial void LogProviderFetch(
        ILogger logger,
        string outcome,
        string mediaType,
        int tmdbId,
        int statusCode,
        long latencyMs,
        int? rateRemaining);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "External ratings provider rate limit low or exceeded for {MediaType} tmdb:{TmdbId} status:{StatusCode} rateRemaining:{RateRemaining}")]
    public static partial void LogRateLimitWarning(
        ILogger logger,
        string mediaType,
        int tmdbId,
        int statusCode,
        int? rateRemaining);
}
