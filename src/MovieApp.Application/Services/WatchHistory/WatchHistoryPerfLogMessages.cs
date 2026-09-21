using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.WatchHistory;

internal static partial class WatchHistoryPerfLogMessages
{
    [LoggerMessage(
        EventId = 8201,
        Level = LogLevel.Debug,
        Message = "WatchHistoryPerf TvShowHydration TvShowId={TvShowId} TotalMs={TotalMs} SummaryHydrationMs={SummaryHydrationMs} SeasonLookupMs={SeasonLookupMs} SeasonsNeedingHydration={SeasonsNeedingHydration} SeasonsHydrated={SeasonsHydrated}")]
    public static partial void LogTvShowHydration(
        ILogger logger,
        Guid tvShowId,
        long totalMs,
        long summaryHydrationMs,
        long seasonLookupMs,
        int seasonsNeedingHydration,
        int seasonsHydrated);

    [LoggerMessage(
        EventId = 8202,
        Level = LogLevel.Debug,
        Message = "WatchHistoryPerf TvShowProgress TvShowId={TvShowId} TotalMs={TotalMs} ExistenceCheckMs={ExistenceCheckMs} ProgressQueryMs={ProgressQueryMs} DbRoundTrips={DbRoundTrips}")]
    public static partial void LogTvShowProgress(
        ILogger logger,
        Guid tvShowId,
        long totalMs,
        long existenceCheckMs,
        long progressQueryMs,
        int dbRoundTrips);
}
