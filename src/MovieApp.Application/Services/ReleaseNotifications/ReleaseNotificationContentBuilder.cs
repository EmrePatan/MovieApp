using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Notifications;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.ReleaseNotifications;

internal static class ReleaseNotificationContentBuilder
{
    public static (string Title, string Body) Build(
        string contentTitle,
        UserReleaseNotificationType notificationType,
        int eventCount)
    {
        var body = ReleaseNotificationBodyLocalization.LocalizeBody(
            notificationType,
            eventCount,
            ContentLocaleResolver.EnglishUnitedStates);

        return (contentTitle, body);
    }
}
