namespace MovieApp.Application.Services.Notifications;

public interface INotificationInboxCleanupService
{
    Task<int> CleanupExpiredReadNotificationsAsync(CancellationToken cancellationToken = default);
}
