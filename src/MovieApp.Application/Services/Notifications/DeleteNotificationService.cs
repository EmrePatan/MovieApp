using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.Notifications;

public sealed class DeleteNotificationService(
    ICurrentUser currentUser,
    IUserReleaseNotificationRepository notificationRepository) : IDeleteNotificationService
{
    public async Task DeleteAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var deleted = await notificationRepository.DeleteAsync(userId, notificationId, cancellationToken);

        if (!deleted)
        {
            throw new NotFoundException("The requested notification was not found.");
        }
    }
}
