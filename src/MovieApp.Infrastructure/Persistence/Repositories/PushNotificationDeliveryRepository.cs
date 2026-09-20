using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class PushNotificationDeliveryRepository(ApplicationDbContext dbContext)
    : IPushNotificationDeliveryRepository
{
    public async Task<int> CreateMissingDeliveriesAsync(
        IReadOnlyCollection<Guid> userReleaseNotificationIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (userReleaseNotificationIds.Count == 0)
        {
            return 0;
        }

        var notifications = await dbContext.UserReleaseNotifications
            .AsNoTracking()
            .Where(notification => userReleaseNotificationIds.Contains(notification.Id))
            .Select(notification => new { notification.Id, notification.UserId })
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return 0;
        }

        var userIds = notifications.Select(notification => notification.UserId).Distinct().ToList();
        var activeDevices = await dbContext.PushDevices
            .AsNoTracking()
            .Where(device => userIds.Contains(device.UserId) && device.IsActive)
            .Select(device => new { device.Id, device.UserId })
            .ToListAsync(cancellationToken);

        var devicesByUser = activeDevices
            .GroupBy(device => device.UserId)
            .ToDictionary(group => group.Key, group => group.Select(device => device.Id).ToList());

        var notificationIds = notifications.Select(notification => notification.Id).ToList();
        var existingPairs = await dbContext.PushNotificationDeliveries
            .AsNoTracking()
            .Where(delivery => notificationIds.Contains(delivery.UserReleaseNotificationId))
            .Select(delivery => new { delivery.UserReleaseNotificationId, delivery.PushDeviceId })
            .ToListAsync(cancellationToken);

        var existingPairSet = existingPairs
            .Select(pair => (pair.UserReleaseNotificationId, pair.PushDeviceId))
            .ToHashSet();

        var pendingStatus = PushNotificationDeliveryStatus.Pending.ToString();
        var created = 0;

        foreach (var notification in notifications)
        {
            if (!devicesByUser.TryGetValue(notification.UserId, out var deviceIds))
            {
                continue;
            }

            foreach (var deviceId in deviceIds)
            {
                if (existingPairSet.Contains((notification.Id, deviceId)))
                {
                    continue;
                }

                var deliveryId = Guid.NewGuid();
                created += await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO push_notification_deliveries (
                         "Id",
                         "UserReleaseNotificationId",
                         "PushDeviceId",
                         "Status",
                         "AttemptCount",
                         "CreatedAtUtc",
                         "UpdatedAtUtc")
                     VALUES (
                         {deliveryId},
                         {notification.Id},
                         {deviceId},
                         {pendingStatus},
                         0,
                         {utcNow},
                         {utcNow})
                     ON CONFLICT ("UserReleaseNotificationId", "PushDeviceId") DO NOTHING
                     """,
                    cancellationToken);

                existingPairSet.Add((notification.Id, deviceId));
            }
        }

        return created;
    }

    public Task<IReadOnlyList<PushNotificationDelivery>> ClaimDueDeliveriesAsync(
        int batchSize,
        DateTime utcNow,
        DateTime claimUntilUtc,
        Guid claimToken,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Database.ExecuteInRetriableTransactionAsync(
            async ct =>
            {
                var pendingStatus = PushNotificationDeliveryStatus.Pending.ToString();
                var retryableStatus = PushNotificationDeliveryStatus.RetryableFailure.ToString();

                var deliveryIds = await dbContext.Database
                    .SqlQuery<Guid>($"""
                        SELECT d."Id" AS "Value"
                        FROM push_notification_deliveries AS d
                        WHERE (
                            d."Status" = {pendingStatus}
                            OR (d."Status" = {retryableStatus} AND d."NextAttemptAtUtc" <= {utcNow})
                        )
                        AND (d."ClaimedUntilUtc" IS NULL OR d."ClaimedUntilUtc" <= {utcNow})
                        ORDER BY d."CreatedAtUtc"
                        LIMIT {batchSize}
                        FOR UPDATE SKIP LOCKED
                        """)
                    .ToListAsync(ct);

                if (deliveryIds.Count == 0)
                {
                    return Array.Empty<PushNotificationDelivery>();
                }

                await dbContext.PushNotificationDeliveries
                    .Where(delivery => deliveryIds.Contains(delivery.Id))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(delivery => delivery.ClaimedUntilUtc, claimUntilUtc)
                            .SetProperty(delivery => delivery.ClaimToken, claimToken)
                            .SetProperty(delivery => delivery.UpdatedAtUtc, utcNow),
                        ct);

                return (IReadOnlyList<PushNotificationDelivery>)await dbContext.PushNotificationDeliveries
                    .Where(delivery => deliveryIds.Contains(delivery.Id) && delivery.ClaimToken == claimToken)
                    .Include(delivery => delivery.PushDevice)
                    .Include(delivery => delivery.UserReleaseNotification)
                        .ThenInclude(notification => notification.NotificationEvents)
                    .ToListAsync(ct);
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<PushNotificationDelivery>> GetSentDeliveriesForReceiptAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.PushNotificationDeliveries
            .Where(delivery =>
                delivery.Status == PushNotificationDeliveryStatus.Sent &&
                delivery.ExpoTicketId != null)
            .OrderBy(delivery => delivery.SentAtUtc)
            .Take(batchSize)
            .Include(delivery => delivery.PushDevice)
            .Include(delivery => delivery.UserReleaseNotification)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveDeliveryUpdatesAsync(
        IReadOnlyCollection<PushNotificationDelivery> deliveries,
        CancellationToken cancellationToken = default)
    {
        if (deliveries.Count == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateNotificationStatusesAsync(
        IReadOnlyCollection<Guid> userReleaseNotificationIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        if (userReleaseNotificationIds.Count == 0)
        {
            return;
        }

        var notifications = await dbContext.UserReleaseNotifications
            .Where(notification => userReleaseNotificationIds.Contains(notification.Id))
            .Include(notification => notification.NotificationEvents)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return;
        }

        var deliveryStatuses = await dbContext.PushNotificationDeliveries
            .AsNoTracking()
            .Where(delivery => userReleaseNotificationIds.Contains(delivery.UserReleaseNotificationId))
            .GroupBy(delivery => delivery.UserReleaseNotificationId)
            .Select(group => new
            {
                NotificationId = group.Key,
                HasDispatched = group.Any(delivery =>
                    delivery.Status == PushNotificationDeliveryStatus.Sent ||
                    delivery.Status == PushNotificationDeliveryStatus.Delivered)
            })
            .ToListAsync(cancellationToken);

        var dispatchedLookup = deliveryStatuses.ToDictionary(
            item => item.NotificationId,
            item => item.HasDispatched);

        foreach (var notification in notifications)
        {
            if (notification.Status != UserReleaseNotificationStatus.Pending)
            {
                continue;
            }

            if (!dispatchedLookup.TryGetValue(notification.Id, out var hasDispatched) || !hasDispatched)
            {
                continue;
            }

            notification.Status = UserReleaseNotificationStatus.Sent;
            notification.SentAtUtc ??= utcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetNotificationIdsNeedingPreparationAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
        {
            return [];
        }

        return await dbContext.Database
            .SqlQuery<Guid>($"""
                SELECT pending."Id" AS "Value"
                FROM (
                    SELECT n."Id", MIN(n."CreatedAtUtc") AS created_at
                    FROM user_release_notifications AS n
                    INNER JOIN push_devices AS d
                      ON d."UserId" = n."UserId"
                     AND d."IsActive" = TRUE
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM push_notification_deliveries AS p
                        WHERE p."UserReleaseNotificationId" = n."Id"
                          AND p."PushDeviceId" = d."Id"
                    )
                    GROUP BY n."Id"
                ) AS pending
                ORDER BY pending.created_at
                LIMIT {batchSize}
                """)
            .ToListAsync(cancellationToken);
    }
}
