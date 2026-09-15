using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Notifications;

namespace MovieApp.Application.Services.Notifications;

public sealed class MarkNotificationReadService(
    ICurrentUser currentUser,
    IUserReleaseNotificationRepository notificationRepository) : IMarkNotificationReadService
{
    public async Task<MarkNotificationReadResult> MarkAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var utcNow = DateTime.UtcNow;

        var result = await notificationRepository.MarkReadAsync(
            userId,
            notificationId,
            utcNow,
            cancellationToken);

        if (result is null)
        {
            throw new NotFoundException("The requested notification was not found.");
        }

        return result;
    }
}
