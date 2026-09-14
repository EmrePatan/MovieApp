using MovieApp.Application.Models.PushNotifications;

namespace MovieApp.Application.Services.PushNotifications;

public interface IPushNotificationDispatchService
{
    Task<PushNotificationDispatchResult> DispatchDueAsync(
        CancellationToken cancellationToken = default);
}
