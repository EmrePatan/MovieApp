using MovieApp.Application.Models.Notifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Mapping;

internal static class NotificationMapper
{
    public static NotificationInboxItemResult ToInboxItemResult(UserReleaseNotification notification) =>
        new(
            notification.Id,
            notification.NotificationType,
            notification.Title ?? string.Empty,
            notification.Body,
            notification.CreatedAtUtc,
            notification.ReadAtUtc,
            ToContentType(notification),
            GetContentId(notification),
            GetPosterPath(notification));

    public static MarkNotificationReadResult ToMarkReadResult(UserReleaseNotification notification) =>
        new(
            notification.Id,
            notification.ReadAtUtc,
            ToContentType(notification),
            GetContentId(notification));

    private static Guid GetContentId(UserReleaseNotification notification) =>
        notification.MovieId ?? notification.TvShowId
        ?? throw new InvalidOperationException("Notification must reference movie or TV show content.");

    private static string ToContentType(UserReleaseNotification notification) =>
        notification.MovieId.HasValue ? "movie" : "tv";

    private static string? GetPosterPath(UserReleaseNotification notification) =>
        notification.MovieId.HasValue
            ? notification.Movie?.PosterPath
            : notification.TvShow?.PosterPath;
}
