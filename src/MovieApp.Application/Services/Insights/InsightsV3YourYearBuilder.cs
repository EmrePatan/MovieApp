using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3YourYearBuilder
{
    public const int MinimumWeekdayActivity = 10;

    public static InsightsV3YourYearResult Build(
        InsightsV3RawData raw,
        TimeZoneInfo timeZone,
        int year)
    {
        var dayCounts = new Dictionary<DateOnly, (int Movies, int Episodes)>();

        foreach (var (watchedAtUtc, _) in raw.YearMovieWatches)
        {
            var date = InsightsV3TimeRangeHelper.ToLocalDate(watchedAtUtc, timeZone);
            if (date.Year != year)
            {
                continue;
            }

            if (!dayCounts.TryGetValue(date, out var counts))
            {
                counts = (0, 0);
            }

            counts.Movies++;
            dayCounts[date] = counts;
        }

        foreach (var (watchedAtUtc, _) in raw.YearEpisodeWatches)
        {
            var date = InsightsV3TimeRangeHelper.ToLocalDate(watchedAtUtc, timeZone);
            if (date.Year != year)
            {
                continue;
            }

            if (!dayCounts.TryGetValue(date, out var counts))
            {
                counts = (0, 0);
            }

            counts.Episodes++;
            dayCounts[date] = counts;
        }

        var monthly = new Dictionary<int, (int Movies, int Episodes)>();
        foreach (var (date, counts) in dayCounts)
        {
            var key = date.Month;
            if (!monthly.TryGetValue(key, out var monthCounts))
            {
                monthCounts = (0, 0);
            }

            monthCounts.Movies += counts.Movies;
            monthCounts.Episodes += counts.Episodes;
            monthly[key] = monthCounts;
        }

        var months = Enumerable.Range(1, 12)
            .Select(month =>
            {
                var counts = monthly.TryGetValue(month, out var value) ? value : (Movies: 0, Episodes: 0);
                return new InsightsV3MonthlyActivityResult(
                    year,
                    month,
                    counts.Movies,
                    counts.Episodes,
                    counts.Movies + counts.Episodes);
            })
            .ToList();

        var activeDays = dayCounts.Count;
        var peakMonth = months
            .Where(month => month.Total > 0)
            .OrderByDescending(month => month.Total)
            .ThenByDescending(month => month.Month)
            .Select(month => new InsightsV3MonthHighlightResult(
                month.Year,
                month.Month,
                month.Movies,
                month.Episodes,
                month.Total))
            .FirstOrDefault();

        var favoriteWeekday = CalculateFavoriteWeekday(dayCounts);

        return new InsightsV3YourYearResult(months, activeDays, peakMonth, favoriteWeekday);
    }

    public static DayOfWeek? CalculateFavoriteWeekday(
        IReadOnlyDictionary<DateOnly, (int Movies, int Episodes)> dayCounts)
    {
        var activeDays = dayCounts
            .Select(pair => new InsightsActivityDayResult(
                pair.Key,
                pair.Value.Movies,
                pair.Value.Episodes,
                pair.Value.Movies + pair.Value.Episodes,
                InsightsActivityDayState.Active,
                0))
            .ToList();

        return InsightsActivityBuilder.CalculateMostActiveWeekday(activeDays);
    }
}
