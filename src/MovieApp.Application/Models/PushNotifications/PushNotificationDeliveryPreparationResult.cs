namespace MovieApp.Application.Models.PushNotifications;

public sealed record PushNotificationDeliveryPreparationResult(
    int NotificationsProcessed,
    int DeliveriesCreated);
