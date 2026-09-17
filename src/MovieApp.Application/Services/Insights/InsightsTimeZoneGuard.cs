using MovieApp.Application.Exceptions;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Application.Services.Insights;

public static class InsightsTimeZoneGuard
{
    public static TimeZoneInfo RequireValidTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new ValidationException("A valid IANA time zone is required.");
        }

        if (!ProfileWatchDateHelper.TryGetTimeZone(timeZoneId, out var timeZone))
        {
            throw new ValidationException($"Invalid time zone '{timeZoneId.Trim()}'.");
        }

        return timeZone;
    }

    public static DateTime GetActivityUtcStart(TimeZoneInfo timeZone, DateTime utcNow)
    {
        var todayLocal = ToLocalDate(utcNow, timeZone);
        var windowStartLocal = todayLocal.AddDays(-(InsightsActivityBuilder.WindowDays - 1));
        var localDateTime = DateTime.SpecifyKind(
            windowStartLocal.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
    }

    private static DateOnly ToLocalDate(DateTime timestampUtc, TimeZoneInfo timeZone)
    {
        var utc = timestampUtc.Kind == DateTimeKind.Utc
            ? timestampUtc
            : DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }
}
