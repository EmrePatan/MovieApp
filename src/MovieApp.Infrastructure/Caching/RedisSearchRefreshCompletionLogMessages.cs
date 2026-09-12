using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Caching;

internal static partial class RedisSearchRefreshCompletionLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Failed to publish search refresh completion signal for key {CompletionKey}.")]
    public static partial void LogPublishFailed(ILogger logger, string completionKey, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Failed to read search refresh completion signal for key {CompletionKey}.")]
    public static partial void LogReadFailed(ILogger logger, string completionKey, Exception exception);
}
