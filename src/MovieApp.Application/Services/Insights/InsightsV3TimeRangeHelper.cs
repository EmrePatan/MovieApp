namespace MovieApp.Application.Services.Insights;

public static class InsightsV3TimeRangeHelper
{
    public static (DateTime UtcStartInclusive, DateTime UtcEndExclusive) GetCalendarYearUtcBounds(
        int year,
        TimeZoneInfo timeZone)
    {
        var localStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localEndExclusive = new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEndExclusive, timeZone));
    }

    public static int ResolveYear(int? requestedYear, TimeZoneInfo timeZone, DateTime utcNow)
    {
        if (requestedYear.HasValue)
        {
            return requestedYear.Value;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(
            utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            timeZone);

        return localNow.Year;
    }

    public static DateOnly ToLocalDate(DateTime timestampUtc, TimeZoneInfo timeZone)
    {
        var utc = timestampUtc.Kind == DateTimeKind.Utc
            ? timestampUtc
            : DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }
}
