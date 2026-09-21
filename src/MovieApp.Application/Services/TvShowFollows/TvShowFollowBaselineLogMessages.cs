using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.TvShowFollows;

internal static partial class TvShowFollowBaselineLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "TV follow baseline completed for show {TvShowId} in {ElapsedMs}ms (summary={SummaryHydrationMs}ms, seasons={SeasonHydrationMs}ms, releaseScan={ReleaseScanMs}ms, seasonsHydrated={SeasonsHydrated}).")]
    internal static partial void LogBaselineCompleted(
        ILogger logger,
        Guid tvShowId,
        long elapsedMs,
        long summaryHydrationMs,
        long seasonHydrationMs,
        long releaseScanMs,
        int seasonsHydrated);
}
