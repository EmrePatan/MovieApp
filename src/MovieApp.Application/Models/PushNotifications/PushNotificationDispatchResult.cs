namespace MovieApp.Application.Models.PushNotifications;

public sealed record PushNotificationDispatchResult(
    int ClaimedCount,
    int SentCount,
    int SkippedCount,
    int RetryableFailureCount,
    int PermanentFailureCount);
