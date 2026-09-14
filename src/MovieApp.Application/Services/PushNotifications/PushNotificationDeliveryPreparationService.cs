using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.PushNotifications;

namespace MovieApp.Application.Services.PushNotifications;

public sealed class PushNotificationDeliveryPreparationService(
    IPushNotificationDeliveryRepository deliveryRepository) : IPushNotificationDeliveryPreparationService
{
    public async Task<PushNotificationDeliveryPreparationResult> PrepareAsync(
        IReadOnlyCollection<Guid> userReleaseNotificationIds,
        CancellationToken cancellationToken = default)
    {
        if (userReleaseNotificationIds.Count == 0)
        {
            return new PushNotificationDeliveryPreparationResult(0, 0);
        }

        var created = await deliveryRepository.CreateMissingDeliveriesAsync(
            userReleaseNotificationIds,
            DateTime.UtcNow,
            cancellationToken);

        return new PushNotificationDeliveryPreparationResult(
            userReleaseNotificationIds.Count,
            created);
    }
}
