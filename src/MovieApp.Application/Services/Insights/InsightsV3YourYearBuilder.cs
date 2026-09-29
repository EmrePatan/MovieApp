using MovieApp.Application.Models.Insights;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3YourYearBuilder
{
    public const int MinimumWeekdayActivity = 10;

    public static InsightsV3YourYearResult Build(InsightsV3RawData raw, int year)
    {
        var monthly = raw.YearActivity.Months.ToDictionary(month => month.Month);
        var months = Enumerable.Range(1, 12)
            .Select(month =>
            {
                monthly.TryGetValue(month, out var counts);
                var movies = counts?.Movies ?? 0;
                var episodes = counts?.Episodes ?? 0;
                return new InsightsV3MonthlyActivityResult(
                    year,
                    month,
                    movies,
                    episodes,
                    movies + episodes);
            })
            .ToList();

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

        return new InsightsV3YourYearResult(
            months,
            raw.YearActivity.ActiveDays,
            peakMonth,
            CalculateFavoriteWeekday(raw.YearActivity.WeekdayTotals));
    }

    public static DayOfWeek? CalculateFavoriteWeekday(IReadOnlyDictionary<DayOfWeek, int> weekdayTotals)
    {
        var sunday = new DateOnly(2024, 1, 7);
        var activeDays = weekdayTotals
            .Where(pair => pair.Value > 0)
            .Select(pair =>
            {
                var date = sunday.AddDays((int)pair.Key);
                return new InsightsActivityDayResult(
                    date,
                    pair.Value,
                    0,
                    pair.Value,
                    InsightsActivityDayState.Active,
                    0);
            })
            .ToList();

        return InsightsActivityBuilder.CalculateMostActiveWeekday(activeDays);
    }
}
