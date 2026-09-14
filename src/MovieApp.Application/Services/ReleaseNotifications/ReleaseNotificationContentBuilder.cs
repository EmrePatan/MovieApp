using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ReleaseNotifications;

internal static class ReleaseNotificationContentBuilder
{
    public static (string Title, string Body) Build(
        string tvShowTitle,
        UserReleaseNotificationType notificationType,
        int eventCount)
    {
        var body = notificationType switch
        {
            UserReleaseNotificationType.NewEpisodes =>
                eventCount == 1 ? "1 new episode" : $"{eventCount} new episodes",
            UserReleaseNotificationType.NewSeason =>
                eventCount == 1 ? "New season premiere" : $"{eventCount} new season premieres",
            _ => string.Empty
        };

        return (tvShowTitle, body);
    }
}
