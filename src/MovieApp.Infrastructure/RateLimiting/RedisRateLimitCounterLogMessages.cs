using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.RateLimiting;

internal static partial class RedisRateLimitCounterLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Redis rate limit acquisition failed for partition {PartitionKey}.")]
    public static partial void LogAcquireFailed(ILogger logger, string partitionKey, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Falling back to in-memory rate limiting for partition {PartitionKey}.")]
    public static partial void LogFallbackToInMemory(ILogger logger, string partitionKey, Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Redis rate limiting unavailable in production for partition {PartitionKey}. Denying request.")]
    public static partial void LogProductionUnavailable(ILogger logger, string partitionKey, Exception exception);
}
