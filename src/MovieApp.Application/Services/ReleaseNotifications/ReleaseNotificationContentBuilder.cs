using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ReleaseNotifications;

internal static class ReleaseNotificationContentBuilder
{
    public static (string Title, string Body) Build(
        string contentTitle,
        UserReleaseNotificationType notificationType,
        int eventCount)
    {
        var body = notificationType switch
        {
            UserReleaseNotificationType.NewEpisodes =>
                eventCount == 1 ? "1 new episode" : $"{eventCount} new episodes",
            UserReleaseNotificationType.NewSeason =>
                eventCount == 1 ? "New season premiere" : $"{eventCount} new season premieres",
            UserReleaseNotificationType.MovieReleased =>
                eventCount == 1 ? "Now available" : $"{eventCount} releases",
            _ => string.Empty
        };

        return (contentTitle, body);
    }
}
