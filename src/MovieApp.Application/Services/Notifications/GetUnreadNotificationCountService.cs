using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.Notifications;

public sealed class GetUnreadNotificationCountService(
    ICurrentUser currentUser,
    IUserReleaseNotificationRepository notificationRepository) : IGetUnreadNotificationCountService
{
    public async Task<int> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        return await notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
    }
}
