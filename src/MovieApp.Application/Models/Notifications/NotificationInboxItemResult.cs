using MovieApp.Domain.Enums;

namespace MovieApp.Application.Models.Notifications;

public sealed record NotificationInboxItemResult(
    Guid Id,
    UserReleaseNotificationType NotificationType,
    string Title,
    string? Body,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc,
    string ContentType,
    Guid ContentId,
    string? PosterPath);
