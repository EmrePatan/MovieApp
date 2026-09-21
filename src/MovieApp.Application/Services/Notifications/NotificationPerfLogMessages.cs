using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Notifications;

internal static partial class NotificationPerfLogMessages
{
    [LoggerMessage(
        EventId = 8501,
        Level = LogLevel.Debug,
        Message = "NotificationPerf UnreadCount TotalMs={TotalMs} RepositoryMs={RepositoryMs} Count={Count}")]
    public static partial void LogUnreadCount(
        ILogger logger,
        long totalMs,
        long repositoryMs,
        int count);
}
