using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Messages;

internal static partial class DirectMessageLogMessages
{
    [LoggerMessage(
        EventId = 7401,
        Level = LogLevel.Warning,
        Message = "Direct message push was rejected for conversation {ConversationId}.")]
    public static partial void LogPushRejected(ILogger logger, Guid conversationId);

    [LoggerMessage(
        EventId = 7402,
        Level = LogLevel.Warning,
        Message = "Direct message push failed for conversation {ConversationId}.")]
    public static partial void LogPushFailed(ILogger logger, Exception exception, Guid conversationId);
}
