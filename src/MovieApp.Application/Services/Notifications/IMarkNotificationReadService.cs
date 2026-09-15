using MovieApp.Application.Models.Notifications;

namespace MovieApp.Application.Services.Notifications;

public interface IMarkNotificationReadService
{
    Task<MarkNotificationReadResult> MarkAsync(
        Guid notificationId,
        CancellationToken cancellationToken = default);
}
