namespace MovieApp.Application.Models.PushNotifications;

public sealed record PushNotificationMessage(
    Guid DeliveryId,
    string ExpoPushToken,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data);
