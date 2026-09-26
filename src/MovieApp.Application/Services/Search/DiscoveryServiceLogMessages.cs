using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Search;

internal static partial class DiscoveryServiceLogMessages
{
    [LoggerMessage(
        EventId = 7201,
        Level = LogLevel.Information,
        Message = "DiscoveryPerf Cache=LOAD_COMPLETED Operation={Operation} CacheKey={CacheKey} StampedeRole={StampedeRole} CacheLookupMs={CacheLookupMs} CanonicalLoadMs={CanonicalLoadMs} OverlayMs={OverlayMs} CacheWriteMs={CacheWriteMs} TotalLoadMs={TotalLoadMs} ItemCount={ItemCount}")]
    public static partial void LogCacheLoadCompleted(
        ILogger logger,
        string operation,
        string cacheKey,
        string stampedeRole,
        long cacheLookupMs,
        long canonicalLoadMs,
        long overlayMs,
        long cacheWriteMs,
        long totalLoadMs,
        int itemCount);

    [LoggerMessage(
        EventId = 7203,
        Level = LogLevel.Debug,
        Message = "DiscoveryPerf Cache=WAIT_FILLED Operation={Operation} CacheKey={CacheKey}")]
    public static partial void LogCacheWaitFilled(
        ILogger logger,
        string operation,
        string cacheKey);

    [LoggerMessage(
        EventId = 7204,
        Level = LogLevel.Information,
        Message = "DiscoveryCachePerf Operation={Operation} Role={Role} InitialCacheLookupMs={InitialCacheLookupMs} LockAcquireMs={LockAcquireMs} WaitForOwnerMs={WaitForOwnerMs} PollCount={PollCount} PostWaitCacheLookupMs={PostWaitCacheLookupMs} FallbackLoadMs={FallbackLoadMs} TotalMs={TotalMs}")]
    public static partial void LogDiscoveryCachePerf(
        ILogger logger,
        string operation,
        string role,
        long initialCacheLookupMs,
        long lockAcquireMs,
        long waitForOwnerMs,
        int pollCount,
        long postWaitCacheLookupMs,
        long fallbackLoadMs,
        long totalMs);

    [LoggerMessage(
        EventId = 7202,
        Level = LogLevel.Information,
        Message = "DiscoveryPerf Cache=LOAD_FAILED Operation={Operation} CacheKey={CacheKey} CacheLookupMs={CacheLookupMs} FailurePhase={FailurePhase} ElapsedMs={ElapsedMs}")]
    public static partial void LogCacheLoadFailed(
        ILogger logger,
        string operation,
        string cacheKey,
        long cacheLookupMs,
        string failurePhase,
        long elapsedMs);
}
