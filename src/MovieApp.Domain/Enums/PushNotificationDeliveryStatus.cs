namespace MovieApp.Domain.Enums;

public enum PushNotificationDeliveryStatus
{
    Pending,
    Sent,
    Delivered,
    RetryableFailure,
    PermanentFailure
}
