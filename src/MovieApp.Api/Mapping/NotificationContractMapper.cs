using MovieApp.Application.Models.Notifications;
using MovieApp.Application.Services.Notifications;
using MovieApp.Contracts.Notifications;
using MovieApp.Domain.Enums;

namespace MovieApp.Api.Mapping;

public static class NotificationContractMapper
{
    public static NotificationsResponse ToNotificationsResponse(
        NotificationsListResult result,
        string contentLocale) =>
        new(
            result.Items.Select(item => ToNotificationItemResponse(item, contentLocale)).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

    public static UnreadNotificationCountResponse ToUnreadCountResponse(int unreadCount) =>
        new(unreadCount);

    public static MarkNotificationReadResponse ToMarkReadResponse(MarkNotificationReadResult result) =>
        new(result.Id, result.ReadAtUtc, result.ContentType, result.ContentId);

    public static MarkAllNotificationsReadResponse ToMarkAllReadResponse(
        MarkAllNotificationsReadResult result) =>
        new(result.AffectedCount);

    private static NotificationItemResponse ToNotificationItemResponse(
        NotificationInboxItemResult item,
        string contentLocale) =>
        new(
            item.Id,
            ToNotificationTypeString(item.NotificationType),
            item.Title,
            ReleaseNotificationBodyLocalization.LocalizeBody(
                item.NotificationType,
                item.Body,
                contentLocale),
            item.CreatedAtUtc,
            item.ReadAtUtc,
            item.ContentType,
            item.ContentId,
            item.PosterPath);

    private static string ToNotificationTypeString(UserReleaseNotificationType notificationType) =>
        notificationType switch
        {
            UserReleaseNotificationType.MovieReleased => "MovieReleased",
            UserReleaseNotificationType.NewSeason => "NewSeason",
            UserReleaseNotificationType.NewEpisodes => "NewEpisodes",
            _ => notificationType.ToString()
        };
}
