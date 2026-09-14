namespace MovieApp.Application.Models.ReleaseNotifications;

public sealed record ReleaseNotificationFanoutResult(
    int EventsProcessed,
    int EligibleFollowers,
    int NotificationsCreated,
    int EventLinksCreated,
    int SkippedByPreference,
    int SkippedByBoundary,
    int SkippedBySource)
{
    public static ReleaseNotificationFanoutResult Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
}
