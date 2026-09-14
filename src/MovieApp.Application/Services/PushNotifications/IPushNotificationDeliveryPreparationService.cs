using MovieApp.Application.Models.PushNotifications;

namespace MovieApp.Application.Services.PushNotifications;

public interface IPushNotificationDeliveryPreparationService
{
    Task<PushNotificationDeliveryPreparationResult> PrepareAsync(
        IReadOnlyCollection<Guid> userReleaseNotificationIds,
        CancellationToken cancellationToken = default);
}
