namespace MovieApp.Application.Models.Notifications;

public sealed record MarkNotificationReadResult(
    Guid Id,
    DateTime? ReadAtUtc,
    string ContentType,
    Guid ContentId);
