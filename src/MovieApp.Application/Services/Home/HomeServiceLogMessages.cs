using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Home;

internal static partial class HomeServiceLogMessages
{
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Information,
        Message = "HomePerf Cache={CacheResult} TotalMs={TotalMs} CacheLookupMs={CacheLookupMs}")]
    public static partial void LogCacheHit(
        ILogger logger,
        string cacheResult,
        long totalMs,
        long cacheLookupMs);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Information,
        Message = "HomePerf Cache={CacheResult} TotalMs={TotalMs} CacheLookupMs={CacheLookupMs} CacheWriteMs={CacheWriteMs} HotThisWeekMs={HotThisWeekMs} RecommendedForYouMs={RecommendedForYouMs} ComingUpMs={ComingUpMs} TrendingMs={TrendingMs} TopRatedMs={TopRatedMs} NewReleasesMs={NewReleasesMs}")]
    public static partial void LogCacheMiss(
        ILogger logger,
        string cacheResult,
        long totalMs,
        long cacheLookupMs,
        long cacheWriteMs,
        long hotThisWeekMs,
        long recommendedForYouMs,
        long comingUpMs,
        long trendingMs,
        long topRatedMs,
        long newReleasesMs);
}
