using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Home;

internal static partial class HotThisWeekTrendingSnapshotLogMessages
{
    [LoggerMessage(
        EventId = 7010,
        Level = LogLevel.Information,
        Message = "HotThisWeek snapshot refresh completed: providerItems={ProviderItemCount} mappedItems={MappedItemCount} skippedItems={SkippedItemCount} refreshedAt={RefreshedAt}")]
    internal static partial void LogRefreshCompleted(
        ILogger logger,
        int providerItemCount,
        int mappedItemCount,
        int skippedItemCount,
        DateTimeOffset refreshedAt);

    [LoggerMessage(
        EventId = 7011,
        Level = LogLevel.Warning,
        Message = "HotThisWeek snapshot refresh skipped: providerItems={ProviderItemCount} mappedItems={MappedItemCount} skippedItems={SkippedItemCount}")]
    internal static partial void LogRefreshSkippedNoUsableData(
        ILogger logger,
        int providerItemCount,
        int mappedItemCount,
        int skippedItemCount);

    [LoggerMessage(
        EventId = 7012,
        Level = LogLevel.Error,
        Message = "HotThisWeek snapshot refresh failed while calling trending week provider")]
    internal static partial void LogRefreshProviderFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 7013,
        Level = LogLevel.Information,
        Message = "HotThisWeek read source={ReadSource} itemCount={ItemCount} snapshotRefreshedAt={SnapshotRefreshedAt}")]
    internal static partial void LogReadSource(
        ILogger logger,
        string readSource,
        int itemCount,
        DateTimeOffset? snapshotRefreshedAt);

    [LoggerMessage(
        EventId = 7014,
        Level = LogLevel.Information,
        Message = "HotThisWeekCachePerf Role={Role} CacheLookupMs={CacheLookupMs} SnapshotLookupMs={SnapshotLookupMs} TrendingFallbackMs={TrendingFallbackMs} WaitMs={WaitMs} TotalMs={TotalMs} ItemCount={ItemCount}")]
    internal static partial void LogCachePerf(
        ILogger logger,
        string role,
        long cacheLookupMs,
        long snapshotLookupMs,
        long trendingFallbackMs,
        long waitMs,
        long totalMs,
        int itemCount);
}
