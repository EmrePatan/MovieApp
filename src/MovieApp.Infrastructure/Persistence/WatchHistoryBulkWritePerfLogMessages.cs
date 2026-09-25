using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Persistence;

internal static partial class WatchHistoryBulkWritePerfLogMessages
{
    [LoggerMessage(
        EventId = 9201,
        Level = LogLevel.Information,
        Message = "WatchHistoryPerf TvWatchStateBulkWrite Operation={Operation} EpisodeCount={EpisodeCount} ConnectionOpenMs={ConnectionOpenMs} ExecuteMs={ExecuteMs} TotalMs={TotalMs} ConnectionWasOpen={ConnectionWasOpen}")]
    public static partial void LogBulkWrite(
        ILogger logger,
        string operation,
        int episodeCount,
        long connectionOpenMs,
        long executeMs,
        long totalMs,
        bool connectionWasOpen);
}
