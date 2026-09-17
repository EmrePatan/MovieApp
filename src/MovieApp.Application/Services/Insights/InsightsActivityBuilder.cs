using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Application.Services.Insights;

public static class InsightsActivityBuilder
{
    public const int WindowDays = 364;

    public static InsightsActivityResult Build(
        InsightsAnalyticsRawData raw,
        TimeZoneInfo timeZone,
        DateTime utcNow)
    {
        var todayLocal = ToLocalDate(utcNow, timeZone);
        var windowStartLocal = todayLocal.AddDays(-(WindowDays - 1));
        var memberSinceLocal = ToLocalDate(raw.MemberSinceUtc, timeZone);

        var dayCounts = new Dictionary<DateOnly, (int Movies, int Episodes)>();
        foreach (var eventData in raw.ActivityEvents)
        {
            var localDate = ToLocalDate(eventData.WatchedAtUtc, timeZone);
            if (localDate < windowStartLocal || localDate > todayLocal)
            {
                continue;
            }

            if (!dayCounts.TryGetValue(localDate, out var counts))
            {
                counts = (0, 0);
            }

            if (eventData.IsMovie)
            {
                counts.Movies++;
            }
            else
            {
                counts.Episodes++;
            }

            dayCounts[localDate] = counts;
        }

        var days = new List<InsightsActivityDayResult>(WindowDays);
        var maxTotal = 0;

        for (var offset = 0; offset < WindowDays; offset++)
        {
            var date = windowStartLocal.AddDays(offset);
            var state = date < memberSinceLocal
                ? InsightsActivityDayState.BeforeJoin
                : InsightsActivityDayState.NoActivity;

            var movies = 0;
            var episodes = 0;
            if (dayCounts.TryGetValue(date, out var counts))
            {
                movies = counts.Movies;
                episodes = counts.Episodes;
            }

            var total = movies + episodes;
            if (state != InsightsActivityDayState.BeforeJoin && total > 0)
            {
                state = InsightsActivityDayState.Active;
                maxTotal = Math.Max(maxTotal, total);
            }

            days.Add(new InsightsActivityDayResult(
                date,
                movies,
                episodes,
                total,
                state,
                0));
        }

        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var intensityBucket = day.State == InsightsActivityDayState.Active
                ? CalculateIntensityBucket(day.Total, maxTotal)
                : 0;
            days[index] = day with { IntensityBucket = intensityBucket };
        }

        var activeDays = days
            .Where(day => day.State == InsightsActivityDayState.Active)
            .ToList();

        var summary = new InsightsActivitySummaryResult(
            activeDays.Count,
            CalculateMostActiveWeekday(activeDays),
            CalculateLongestStreak(activeDays.Select(day => day.Date).ToList()),
            CalculateWeekTotal(activeDays, todayLocal, 0),
            CalculateWeekTotal(activeDays, todayLocal, 1));

        return new InsightsActivityResult(days, summary);
    }

    public static int CalculateIntensityBucket(int total, int maxTotal)
    {
        if (total <= 0 || maxTotal <= 0)
        {
            return 0;
        }

        return Math.Clamp((int)Math.Ceiling(total * 4d / maxTotal), 1, 4);
    }

    public static DayOfWeek? CalculateMostActiveWeekday(IReadOnlyList<InsightsActivityDayResult> activeDays)
    {
        var totalActivity = activeDays.Sum(day => day.Total);
        if (totalActivity < 10)
        {
            return null;
        }

        var weekdayTotals = activeDays
            .GroupBy(day => day.Date.DayOfWeek)
            .ToDictionary(group => group.Key, group => group.Sum(day => day.Total));

        var ordered = weekdayTotals
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .ToList();

        if (ordered.Count == 0)
        {
            return null;
        }

        var winner = ordered[0];
        var otherWeekdays = weekdayTotals
            .Where(pair => pair.Key != winner.Key)
            .Select(pair => pair.Value)
            .ToList();

        if (otherWeekdays.Count == 0)
        {
            return winner.Key;
        }

        var otherMean = otherWeekdays.Average();
        if (otherMean <= 0 || winner.Value < otherMean * 1.5d)
        {
            return null;
        }

        return winner.Key;
    }

    public static int? CalculateLongestStreak(IReadOnlyList<DateOnly> activeDates)
    {
        return ProfileStatisticsBuilder.CalculateLongestStreakDays(activeDates);
    }

    private static int CalculateWeekTotal(
        IReadOnlyList<InsightsActivityDayResult> activeDays,
        DateOnly todayLocal,
        int weeksAgo)
    {
        var weekEnd = todayLocal.AddDays(-(weeksAgo * 7));
        var weekStart = weekEnd.AddDays(-6);

        return activeDays
            .Where(day => day.Date >= weekStart && day.Date <= weekEnd)
            .Sum(day => day.Total);
    }

    private static DateOnly ToLocalDate(DateTime timestampUtc, TimeZoneInfo timeZone)
    {
        var utc = timestampUtc.Kind == DateTimeKind.Utc
            ? timestampUtc
            : DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone));
    }
}
