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
}
