using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;

namespace MovieApp.Application.Services.Notifications;

public sealed class NotificationInboxCleanupService(
    IUserReleaseNotificationRepository notificationRepository,
    IOptions<NotificationRetentionOptions> options) : INotificationInboxCleanupService
{
    public Task<int> CleanupExpiredReadNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var cutoffUtc = NotificationInboxRetention.GetReadExpirationCutoffUtc(
            DateTime.UtcNow,
            options.Value.ReadRetentionDays);

        return notificationRepository.DeleteExpiredReadNotificationsAsync(cutoffUtc, cancellationToken);
    }
}
