namespace MovieApp.Application.Models.PushNotifications;

public sealed record PushNotificationReceiptResult(
    int ProcessedCount,
    int DeliveredCount,
    int RetryableFailureCount,
    int PermanentFailureCount);
