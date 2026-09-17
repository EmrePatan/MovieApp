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

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Information,
        Message = "HomePerf Browse TotalMs={TotalMs} HotThisWeekMs={HotThisWeekMs} TrendingMs={TrendingMs} TopRatedMs={TopRatedMs} NewReleasesMs={NewReleasesMs} SectionCount={SectionCount}")]
    public static partial void LogBrowse(
        ILogger logger,
        long totalMs,
        long hotThisWeekMs,
        long trendingMs,
        long topRatedMs,
        long newReleasesMs,
        int sectionCount);

    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Information,
        Message = "HomePerf Personalized TotalMs={TotalMs} HotThisWeekDedupMs={HotThisWeekDedupMs} ComingUpMs={ComingUpMs} RecommendedForYouMs={RecommendedForYouMs} IsPersonalized={IsPersonalized} SectionCount={SectionCount}")]
    public static partial void LogPersonalized(
        ILogger logger,
        long totalMs,
        long hotThisWeekDedupMs,
        long comingUpMs,
        long recommendedForYouMs,
        bool isPersonalized,
        int sectionCount);
}
