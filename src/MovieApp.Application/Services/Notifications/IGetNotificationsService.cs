using MovieApp.Application.Models.Notifications;

namespace MovieApp.Application.Services.Notifications;

public interface IGetNotificationsService
{
    Task<NotificationsListResult> GetAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
