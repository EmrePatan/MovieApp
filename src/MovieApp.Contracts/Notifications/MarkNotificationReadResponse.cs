namespace MovieApp.Contracts.Notifications;

public sealed record MarkNotificationReadResponse(
    Guid Id,
    DateTime? ReadAtUtc,
    string ContentType,
    Guid ContentId);
