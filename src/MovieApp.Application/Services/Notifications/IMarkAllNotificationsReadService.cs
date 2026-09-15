using MovieApp.Application.Models.Notifications;

namespace MovieApp.Application.Services.Notifications;

public interface IMarkAllNotificationsReadService
{
    Task<MarkAllNotificationsReadResult> MarkAllAsync(CancellationToken cancellationToken = default);
}
