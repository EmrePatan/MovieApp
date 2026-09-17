using MovieApp.Application.Models.Notifications;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IUserReleaseNotificationRepository
{
    Task<(IReadOnlyList<NotificationInboxItemResult> Items, int TotalCount)> GetInboxAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<MarkNotificationReadResult?> MarkReadAsync(
        Guid userId,
        Guid notificationId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(
        Guid userId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default);

    Task<int> DeleteExpiredReadNotificationsAsync(
        DateTime readExpirationCutoffUtc,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);
}
