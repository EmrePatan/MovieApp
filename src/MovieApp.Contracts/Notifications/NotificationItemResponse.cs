namespace MovieApp.Contracts.Notifications;

public sealed record NotificationItemResponse(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    string ContentType,
    Guid ContentId,
    string? PosterPath);
