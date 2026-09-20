using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Insights;

internal static partial class InsightsAnalyticsLogMessages
{
    [LoggerMessage(
        EventId = 7102,
        Level = LogLevel.Debug,
        Message = "InsightsPerf Analytics Cache={CacheResult} TotalMs={TotalMs} CacheLookupMs={CacheLookupMs} DbTotalMs={DbTotalMs} DbRoundTrips={DbRoundTrips} ActivityMs={ActivityMs} TasteErasMs={TasteErasMs} RuntimeMs={RuntimeMs} RatingsMs={RatingsMs} MilestonesMs={MilestonesMs} BuildCpuMs={BuildCpuMs} CacheWriteMs={CacheWriteMs} ActivityDays={ActivityDays} TasteGenreCount={TasteGenreCount} MilestoneCount={MilestoneCount}")]
    public static partial void LogAnalyticsRequest(
        ILogger logger,
        string cacheResult,
        long totalMs,
        long cacheLookupMs,
        long dbTotalMs,
        int dbRoundTrips,
        long activityMs,
        long tasteErasMs,
        long runtimeMs,
        long ratingsMs,
        long milestonesMs,
        long buildCpuMs,
        long cacheWriteMs,
        int activityDays,
        int tasteGenreCount,
        int milestoneCount);
}
