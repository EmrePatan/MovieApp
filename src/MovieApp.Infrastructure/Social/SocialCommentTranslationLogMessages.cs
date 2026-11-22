using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Social;

internal static partial class SocialCommentTranslationLogMessages
{
    [LoggerMessage(
        EventId = 6320,
        Level = LogLevel.Warning,
        Message = "Social comment translation quota exceeded for {CommentId}")]
    public static partial void LogQuotaExceeded(ILogger logger, Guid commentId);

    [LoggerMessage(
        EventId = 6321,
        Level = LogLevel.Warning,
        Message = "Social comment translation provider unavailable for {CommentId}")]
    public static partial void LogProviderUnavailable(ILogger logger, Guid commentId);
}
