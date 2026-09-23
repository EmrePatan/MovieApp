using MovieApp.Application.Models.PushNotifications;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Notifications;
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
        var contentLocale = string.IsNullOrWhiteSpace(delivery.PushDevice.ContentLocale)
            ? ContentLocaleResolver.EnglishUnitedStates
            : ContentLocaleResolver.Normalize(delivery.PushDevice.ContentLocale);

        var title = string.IsNullOrWhiteSpace(notification.Title)
            ? notification.NotificationType == UserReleaseNotificationType.MovieReleased
                ? LocalizePushTitleMovie(contentLocale)
                : LocalizePushTitleTv(contentLocale)
            : notification.Title;

        var body = BuildBody(notification, eventCount, contentLocale);

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

    private static string BuildBody(
        UserReleaseNotification notification,
        int eventCount,
        string contentLocale)
    {
        if (!string.IsNullOrWhiteSpace(notification.Body))
        {
            return ReleaseNotificationBodyLocalization.LocalizeBody(
                notification.NotificationType,
                notification.Body,
                contentLocale);
        }

        return ReleaseNotificationBodyLocalization.LocalizeBody(
            notification.NotificationType,
            eventCount,
            contentLocale);
    }

    private static string LocalizePushTitleMovie(string contentLocale) =>
        contentLocale switch
        {
            ContentLocaleResolver.TurkishTurkey => "Film",
            ContentLocaleResolver.SpanishSpain => "Película",
            ContentLocaleResolver.GermanGermany => "Film",
            ContentLocaleResolver.FrenchFrance => "Film",
            ContentLocaleResolver.ItalianItaly => "Film",
            ContentLocaleResolver.PortugueseBrazil => "Filme",
            _ => "Movie",
        };

    private static string LocalizePushTitleTv(string contentLocale) =>
        contentLocale switch
        {
            ContentLocaleResolver.TurkishTurkey => "Dizi",
            ContentLocaleResolver.SpanishSpain => "Serie",
            ContentLocaleResolver.GermanGermany => "Serie",
            ContentLocaleResolver.FrenchFrance => "Série",
            ContentLocaleResolver.ItalianItaly => "Serie TV",
            ContentLocaleResolver.PortugueseBrazil => "Série",
            _ => "TV Show",
        };
}
