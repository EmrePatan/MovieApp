namespace MovieApp.Application.Services.Identity;

public static class ProfileWatchDateHelper
{
    public static bool TryGetTimeZone(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    public static IReadOnlyList<DateOnly> ToDistinctLocalWatchDates(
        IReadOnlyList<DateTime> timestampsUtc,
        TimeZoneInfo timeZone)
    {
        return timestampsUtc
            .Select(timestamp => ToLocalDate(timestamp, timeZone))
            .Distinct()
            .ToList();
    }

    private static DateOnly ToLocalDate(DateTime timestampUtc, TimeZoneInfo timeZone)
    {
        var utc = timestampUtc.Kind == DateTimeKind.Utc
            ? timestampUtc
            : DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }
}
