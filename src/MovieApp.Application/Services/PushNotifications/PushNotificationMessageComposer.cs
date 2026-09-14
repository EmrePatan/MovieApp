using MovieApp.Application.Models.PushNotifications;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.PushNotifications;

internal static class PushNotificationMessageComposer
{
    public static PushNotificationMessage Compose(
        PushNotificationDelivery delivery,
        int eventCount)
    {
        var notification = delivery.UserReleaseNotification;
        var title = string.IsNullOrWhiteSpace(notification.Title)
            ? notification.NotificationType == UserReleaseNotificationType.MovieReleased ? "Movie" : "TV Show"
            : notification.Title;
        var body = BuildBody(notification, eventCount);

        var data = new Dictionary<string, string>
        {
            ["type"] = notification.NotificationType == UserReleaseNotificationType.MovieReleased
                ? "movie-release"
                : "tv-release",
            ["notificationId"] = notification.Id.ToString()
        };

        if (notification.TvShowId.HasValue)
        {
            data["tvShowId"] = notification.TvShowId.Value.ToString();
        }

        if (notification.MovieId.HasValue)
        {
            data["movieId"] = notification.MovieId.Value.ToString();
        }

        return new PushNotificationMessage(
            delivery.Id,
            delivery.PushDevice.ExpoPushToken,
            title,
            body,
            data);
    }

    private static string BuildBody(UserReleaseNotification notification, int eventCount)
    {
        if (!string.IsNullOrWhiteSpace(notification.Body))
        {
            return notification.Body;
        }

        return notification.NotificationType switch
        {
            UserReleaseNotificationType.NewEpisodes =>
                eventCount == 1
                    ? "A new episode is available."
                    : $"{eventCount} new episodes are available.",
            UserReleaseNotificationType.NewSeason => "A new season is available.",
            UserReleaseNotificationType.MovieReleased => "The movie is now available.",
            _ => "New content is available."
        };
    }
}
