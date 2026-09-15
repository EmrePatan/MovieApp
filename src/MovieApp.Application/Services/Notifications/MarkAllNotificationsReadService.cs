using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Notifications;

namespace MovieApp.Application.Services.Notifications;

public sealed class MarkAllNotificationsReadService(
    ICurrentUser currentUser,
    IUserReleaseNotificationRepository notificationRepository) : IMarkAllNotificationsReadService
{
    public async Task<MarkAllNotificationsReadResult> MarkAllAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var affectedCount = await notificationRepository.MarkAllReadAsync(
            userId,
            DateTime.UtcNow,
            cancellationToken);

        return new MarkAllNotificationsReadResult(affectedCount);
    }
}
