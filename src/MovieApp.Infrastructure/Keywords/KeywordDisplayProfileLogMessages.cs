using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Keywords;

internal static partial class KeywordDisplayProfileLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Keyword display profiles refreshed. Written={Written} Displayable={Displayable} DurationMs={DurationMs}")]
    public static partial void LogRefreshCompleted(
        ILogger logger,
        int written,
        int displayable,
        long durationMs);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Keyword display profile refresh failed.")]
    public static partial void LogRefreshFailed(ILogger logger, Exception exception);
}
