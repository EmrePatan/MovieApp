using MovieApp.Application.Models.PushNotifications;

namespace MovieApp.Application.Services.PushNotifications;

public interface IPushNotificationReceiptService
{
    Task<PushNotificationReceiptResult> ProcessReceiptsAsync(
        CancellationToken cancellationToken = default);
}
