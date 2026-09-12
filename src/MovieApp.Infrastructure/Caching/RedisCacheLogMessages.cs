using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Caching;

internal static partial class RedisCacheLogMessages
{
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Warning,
        Message = "Redis cache get failed ({ExceptionType}); continuing without cache.")]
    internal static partial void LogCacheGetFailed(ILogger logger, string exceptionType);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Warning,
        Message = "Redis cache set failed ({ExceptionType}); primary operation result was preserved.")]
    internal static partial void LogCacheSetFailed(ILogger logger, string exceptionType);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Warning,
        Message = "Redis cache remove failed ({ExceptionType}); continuing without cache invalidation.")]
    internal static partial void LogCacheRemoveFailed(ILogger logger, string exceptionType);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Warning,
        Message = "Redis cache is unavailable; subsequent cache failures will be suppressed for {SuppressionSeconds} seconds.")]
    internal static partial void LogCacheUnavailable(
        ILogger logger,
        int suppressionSeconds);
}
