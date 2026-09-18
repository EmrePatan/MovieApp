using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Insights;

internal static partial class InsightsV3LogMessages
{
    [LoggerMessage(
        EventId = 7103,
        Level = LogLevel.Information,
        Message = "Insights V3 cache HIT for user {UserId} in {ElapsedMs}ms (year={Year}, timeZone={TimeZone})")]
    public static partial void LogCacheHit(
        ILogger logger,
        Guid userId,
        long elapsedMs,
        int year,
        string timeZone);

    [LoggerMessage(
        EventId = 7104,
        Level = LogLevel.Information,
        Message = "Insights V3 cache MISS for user {UserId} in {ElapsedMs}ms (db={DbMs}ms, build={BuildMs}ms, roundTrips={RoundTrips}, year={Year})")]
    public static partial void LogCacheMiss(
        ILogger logger,
        Guid userId,
        long elapsedMs,
        long dbMs,
        long buildMs,
        int roundTrips,
        int year);
}
