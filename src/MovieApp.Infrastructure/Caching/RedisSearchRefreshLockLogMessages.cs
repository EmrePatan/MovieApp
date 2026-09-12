using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Caching;

internal static partial class RedisSearchRefreshLockLogMessages
{
    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Warning,
        Message = "Search refresh lock acquisition failed for key {LockKey}. Proceeding without distributed lock.")]
    public static partial void LogLockAcquisitionFailed(
        ILogger logger,
        string lockKey,
        Exception exception);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Warning,
        Message = "Search refresh lock release failed for key {LockKey}. Lock will expire via TTL.")]
    public static partial void LogLockReleaseFailed(
        ILogger logger,
        string lockKey,
        Exception exception);
}
