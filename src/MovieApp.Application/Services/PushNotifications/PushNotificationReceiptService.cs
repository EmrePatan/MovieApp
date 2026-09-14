using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.PushNotifications;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.PushNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.PushNotifications;

public sealed class PushNotificationReceiptService(
    IPushNotificationDeliveryRepository deliveryRepository,
    IExpoPushClient expoPushClient,
    IOptions<PushNotificationsOptions> options) : IPushNotificationReceiptService
{
    public async Task<PushNotificationReceiptResult> ProcessReceiptsAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return new PushNotificationReceiptResult(0, 0, 0, 0);
        }

        var deliveries = await deliveryRepository.GetSentDeliveriesForReceiptAsync(
            settings.DispatchBatchSize,
            cancellationToken);

        if (deliveries.Count == 0)
        {
            return new PushNotificationReceiptResult(0, 0, 0, 0);
        }

        var ticketIds = deliveries
            .Select(delivery => delivery.ExpoTicketId!)
            .Distinct()
            .ToList();

        var receiptResults = await expoPushClient.GetReceiptsAsync(ticketIds, cancellationToken);
        var receiptsByTicketId = receiptResults.ToDictionary(receipt => receipt.TicketId);

        var utcNow = DateTime.UtcNow;
        var delivered = 0;
        var retryableFailures = 0;
        var permanentFailures = 0;
        var notificationIds = new HashSet<Guid>();

        foreach (var delivery in deliveries)
        {
            notificationIds.Add(delivery.UserReleaseNotificationId);

            if (string.IsNullOrWhiteSpace(delivery.ExpoTicketId) ||
                !receiptsByTicketId.TryGetValue(delivery.ExpoTicketId, out var receipt))
            {
                continue;
            }

            ApplyReceiptResult(delivery, receipt, settings.MaxAttempts, utcNow);

            if (receipt.IsDelivered)
            {
                delivered++;
            }
            else if (receipt.IsPermanentFailure)
            {
                permanentFailures++;
                if (ExpoPushErrorClassifier.IsPermanentDeviceError(receipt.ErrorCode))
                {
                    delivery.PushDevice.IsActive = false;
                    delivery.PushDevice.UpdatedAtUtc = utcNow;
                }
            }
            else if (receipt.IsRetryableFailure)
            {
                retryableFailures++;
            }
        }

        await deliveryRepository.SaveDeliveryUpdatesAsync(deliveries, cancellationToken);
        await deliveryRepository.UpdateNotificationStatusesAsync(notificationIds, utcNow, cancellationToken);

        return new PushNotificationReceiptResult(
            deliveries.Count,
            delivered,
            retryableFailures,
            permanentFailures);
    }

    private static void ApplyReceiptResult(
        PushNotificationDelivery delivery,
        ExpoPushReceiptResult receipt,
        int maxAttempts,
        DateTime utcNow)
    {
        delivery.UpdatedAtUtc = utcNow;
        delivery.LastErrorCode = receipt.ErrorCode;
        delivery.LastErrorMessage = receipt.ErrorMessage;

        if (receipt.IsDelivered)
        {
            delivery.Status = PushNotificationDeliveryStatus.Delivered;
            delivery.DeliveredAtUtc = utcNow;
            delivery.NextAttemptAtUtc = null;
            return;
        }

        delivery.AttemptCount++;
        delivery.ExpoTicketId = null;
        delivery.SentAtUtc = null;

        if (receipt.IsPermanentFailure ||
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
}
