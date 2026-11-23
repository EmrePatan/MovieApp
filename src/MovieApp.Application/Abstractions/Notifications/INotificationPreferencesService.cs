namespace MovieApp.Application.Abstractions.Notifications;

public sealed record NotificationPreferencesResult(bool MessagePushEnabled, bool ReleasePushEnabled);

public interface INotificationPreferencesService
{
    Task<NotificationPreferencesResult> GetAsync(CancellationToken cancellationToken = default);

    Task<NotificationPreferencesResult> UpdateAsync(
        bool messagePushEnabled, bool releasePushEnabled, CancellationToken cancellationToken = default);
}
