using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.PushNotifications;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.PushNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.PushNotifications;

public sealed class PushNotificationDispatchService(
    IPushNotificationDeliveryRepository deliveryRepository,
    IExpoPushClient expoPushClient,
    IOptions<PushNotificationsOptions> options) : IPushNotificationDispatchService
{
    private const string DeviceOwnershipMismatchCode = "DeviceOwnershipMismatch";
    private const string DeviceInactiveCode = "DeviceInactive";

    public async Task<PushNotificationDispatchResult> DispatchDueAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return new PushNotificationDispatchResult(0, 0, 0, 0, 0);
        }

        var utcNow = DateTime.UtcNow;
        var claimToken = Guid.NewGuid();
        var claimUntilUtc = utcNow.AddMinutes(settings.ClaimLeaseMinutes);

        var claimedDeliveries = await deliveryRepository.ClaimDueDeliveriesAsync(
            settings.DispatchBatchSize,
            utcNow,
            claimUntilUtc,
            claimToken,
            cancellationToken);

        if (claimedDeliveries.Count == 0)
        {
            return new PushNotificationDispatchResult(0, 0, 0, 0, 0);
        }

        var messages = new List<PushNotificationMessage>();
        var skipped = 0;
        var permanentFailures = 0;
        var notificationIds = new HashSet<Guid>();

        foreach (var delivery in claimedDeliveries)
        {
            notificationIds.Add(delivery.UserReleaseNotificationId);

            if (!delivery.PushDevice.IsActive)
            {
                MarkPermanentFailure(delivery, DeviceInactiveCode, "Push device is inactive.", utcNow);
                permanentFailures++;
                skipped++;
                continue;
            }

            if (delivery.UserReleaseNotification.UserId != delivery.PushDevice.UserId)
            {
                MarkPermanentFailure(
                    delivery,
                    DeviceOwnershipMismatchCode,
                    "Push device ownership no longer matches notification user.",
                    utcNow);
                permanentFailures++;
                skipped++;
                continue;
            }

            var eventCount = delivery.UserReleaseNotification.NotificationEvents.Count;
            messages.Add(PushNotificationMessageComposer.Compose(delivery, eventCount));
        }

        var sent = 0;
        var retryableFailures = 0;

        if (messages.Count > 0)
        {
            var sendResults = await expoPushClient.SendAsync(messages, cancellationToken);
            if (sendResults.Count != messages.Count)
            {
                throw new InvalidOperationException(
                    "Expo push send response count does not match requested message count.");
            }

            var resultsByDeliveryId = sendResults.ToDictionary(result => result.DeliveryId);

            foreach (var message in messages)
            {
                if (!resultsByDeliveryId.TryGetValue(message.DeliveryId, out var result))
                {
                    throw new InvalidOperationException(
                        $"Expo push send response missing delivery {message.DeliveryId}.");
                }

                var delivery = claimedDeliveries.Single(candidate => candidate.Id == message.DeliveryId);
                ApplySendResult(delivery, result, settings.MaxAttempts, utcNow);

                if (result.IsSuccess)
                {
                    sent++;
                }
                else if (result.IsPermanentFailure)
                {
                    permanentFailures++;
                    if (ExpoPushErrorClassifier.IsPermanentDeviceError(result.ErrorCode))
                    {
                        delivery.PushDevice.IsActive = false;
                        delivery.PushDevice.UpdatedAtUtc = utcNow;
                    }
                }
                else if (result.IsRetryableFailure)
                {
                    retryableFailures++;
                }
            }
        }

        await deliveryRepository.SaveDeliveryUpdatesAsync(claimedDeliveries, cancellationToken);
        await deliveryRepository.UpdateNotificationStatusesAsync(notificationIds, utcNow, cancellationToken);

        return new PushNotificationDispatchResult(
            claimedDeliveries.Count,
            sent,
            skipped,
            retryableFailures,
            permanentFailures);
    }

    private static void ApplySendResult(
        PushNotificationDelivery delivery,
        ExpoPushSendResult result,
        int maxAttempts,
        DateTime utcNow)
    {
        delivery.AttemptCount++;
        delivery.UpdatedAtUtc = utcNow;
        delivery.ClaimedUntilUtc = null;
        delivery.ClaimToken = null;
        delivery.LastErrorCode = result.ErrorCode;
        delivery.LastErrorMessage = result.ErrorMessage;

        if (result.IsSuccess)
        {
            delivery.Status = PushNotificationDeliveryStatus.Sent;
            delivery.ExpoTicketId = result.TicketId;
            delivery.SentAtUtc = utcNow;
            delivery.NextAttemptAtUtc = null;
            return;
        }

        delivery.ExpoTicketId = null;
        delivery.SentAtUtc = null;

        if (result.IsPermanentFailure ||
            PushNotificationRetryPolicy.HasExceededMaxAttempts(delivery.AttemptCount, maxAttempts))
        {
            delivery.Status = PushNotificationDeliveryStatus.PermanentFailure;
            delivery.NextAttemptAtUtc = null;
            return;
        }

        delivery.Status = PushNotificationDeliveryStatus.RetryableFailure;
        delivery.NextAttemptAtUtc = PushNotificationRetryPolicy.CalculateNextAttemptUtc(
            delivery.AttemptCount,
            utcNow,
            maxAttempts);
    }

    private static void MarkPermanentFailure(
        PushNotificationDelivery delivery,
        string errorCode,
        string errorMessage,
        DateTime utcNow)
    {
        delivery.Status = PushNotificationDeliveryStatus.PermanentFailure;
        delivery.LastErrorCode = errorCode;
        delivery.LastErrorMessage = errorMessage;
        delivery.NextAttemptAtUtc = null;
        delivery.ClaimedUntilUtc = null;
        delivery.ClaimToken = null;
        delivery.UpdatedAtUtc = utcNow;
    }
}
