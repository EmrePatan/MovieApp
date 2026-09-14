using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IPushNotificationDeliveryRepository
{
    Task<int> CreateMissingDeliveriesAsync(
        IReadOnlyCollection<Guid> userReleaseNotificationIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PushNotificationDelivery>> ClaimDueDeliveriesAsync(
        int batchSize,
        DateTime utcNow,
        DateTime claimUntilUtc,
        Guid claimToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PushNotificationDelivery>> GetSentDeliveriesForReceiptAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    Task SaveDeliveryUpdatesAsync(
        IReadOnlyCollection<PushNotificationDelivery> deliveries,
        CancellationToken cancellationToken = default);

    Task UpdateNotificationStatusesAsync(
        IReadOnlyCollection<Guid> userReleaseNotificationIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetNotificationIdsNeedingPreparationAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}
