using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Insights;

internal static partial class InsightsV3LogMessages
{
    [LoggerMessage(
        EventId = 7103,
        Level = LogLevel.Information,
        Message = "InsightsPerf V3 Cache=HIT TotalMs={TotalMs} CacheLookupMs={CacheLookupMs} UserId={UserId} Year={Year} TimeZone={TimeZone}")]
    public static partial void LogCacheHit(
        ILogger logger,
        Guid userId,
        long totalMs,
        long cacheLookupMs,
        int year,
        string timeZone);

    [LoggerMessage(
        EventId = 7104,
        Level = LogLevel.Information,
        Message = "InsightsPerf V3 Cache=MISS TotalMs={TotalMs} CacheLookupMs={CacheLookupMs} DbTotalMs={DbTotalMs} RepositoryPhases={RepositoryPhases} PgCommandRoundTrips={PgCommandRoundTrips} SummaryMs={SummaryMs} DnaMs={DnaMs} YearActivityMs={YearActivityMs} RecordsMs={RecordsMs} RuntimeMs={RuntimeMs} RatingsMs={RatingsMs} MilestonesMs={MilestonesMs} BuildCpuMs={BuildCpuMs} CacheWriteMs={CacheWriteMs} SourceVersion={SourceVersion} UserId={UserId} Year={Year}")]
    public static partial void LogCacheMiss(
        ILogger logger,
        Guid userId,
        long totalMs,
        long cacheLookupMs,
        long dbTotalMs,
        int repositoryPhases,
        int pgCommandRoundTrips,
        long summaryMs,
        long dnaMs,
        long yearActivityMs,
        long recordsMs,
        long runtimeMs,
        long ratingsMs,
        long milestonesMs,
        long buildCpuMs,
        long cacheWriteMs,
        string sourceVersion,
        int year);
}
