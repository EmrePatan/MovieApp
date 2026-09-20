using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Insights;

internal static partial class InsightsSummaryLogMessages
{
    [LoggerMessage(
        EventId = 7101,
        Level = LogLevel.Debug,
        Message = "InsightsPerf Summary Cache={CacheResult} TotalMs={TotalMs} CacheLookupMs={CacheLookupMs} DbTotalMs={DbTotalMs} DbRoundTrips={DbRoundTrips} BuildCpuMs={BuildCpuMs} CacheWriteMs={CacheWriteMs} MoviesWatched={MoviesWatched} EpisodesWatched={EpisodesWatched} ShowsStarted={ShowsStarted} RatingsCount={RatingsCount} DnaLabelCount={DnaLabelCount}")]
    public static partial void LogSummaryRequest(
        ILogger logger,
        string cacheResult,
        long totalMs,
        long cacheLookupMs,
        long dbTotalMs,
        int dbRoundTrips,
        long buildCpuMs,
        long cacheWriteMs,
        int moviesWatched,
        int episodesWatched,
        int showsStarted,
        int ratingsCount,
        int dnaLabelCount);
}
