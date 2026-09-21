using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.Notifications;

public sealed class GetUnreadNotificationCountService(
    ICurrentUser currentUser,
    IUserReleaseNotificationRepository notificationRepository,
    ILogger<GetUnreadNotificationCountService> logger) : IGetUnreadNotificationCountService
{
    public async Task<int> GetAsync(CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var repositoryStopwatch = Stopwatch.StartNew();
        var count = await notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
        repositoryStopwatch.Stop();
        totalStopwatch.Stop();

        NotificationPerfLogMessages.LogUnreadCount(
            logger,
            totalStopwatch.ElapsedMilliseconds,
            repositoryStopwatch.ElapsedMilliseconds,
            count);

        return count;
    }
}
