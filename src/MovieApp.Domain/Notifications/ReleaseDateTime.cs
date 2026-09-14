namespace MovieApp.Domain.Notifications;

public static class ReleaseDateTime
{
    public static DateTime ToReleaseAtUtc(DateOnly airDate) =>
        new(airDate.Year, airDate.Month, airDate.Day, 0, 0, 0, DateTimeKind.Utc);
}
