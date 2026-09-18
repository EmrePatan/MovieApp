using System.Globalization;
using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Identity;

namespace MovieApp.Application.Services.Insights;

public static class InsightsV3RecordsBuilder
{
    public static InsightsV3RecordsSectionResult Build(InsightsV3RawData raw, TimeZoneInfo timeZone)
    {
        var distinctDates = raw.AllMovieWatchedAtUtc
            .Concat(raw.AllEpisodeWatchedAtUtc)
            .Select(timestamp => InsightsV3TimeRangeHelper.ToLocalDate(timestamp, timeZone))
            .Distinct()
            .ToList();

        var longestStreak = ProfileStatisticsBuilder.CalculateLongestStreakDays(distinctDates);
        var bestMovieWeek = CalculateBestWeek(raw.AllMovieWatchedAtUtc, timeZone);
        var bestEpisodeWeek = CalculateBestWeek(raw.AllEpisodeWatchedAtUtc, timeZone);
        var highestRatingStars = InsightsV3RatingsBuilder.CalculateHighestRatingStars(raw.RatingScoreCounts);

        return new InsightsV3RecordsSectionResult(
            longestStreak,
            bestMovieWeek,
            bestEpisodeWeek,
            highestRatingStars);
    }

    public static InsightsV3WeeklyPeakResult? CalculateBestWeek(
        IReadOnlyList<DateTime> watchedAtUtc,
        TimeZoneInfo timeZone)
    {
        if (watchedAtUtc.Count == 0)
        {
            return null;
        }

        var weeklyCounts = watchedAtUtc
            .Select(timestamp => InsightsV3TimeRangeHelper.ToLocalDate(timestamp, timeZone))
            .GroupBy(date => ISOWeek.GetYear(date.ToDateTime(TimeOnly.MinValue)) * 100 + ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)))
            .Select(group =>
            {
                var sampleDate = group.First();
                var dateTime = sampleDate.ToDateTime(TimeOnly.MinValue);
                return new InsightsV3WeeklyPeakResult(
                    ISOWeek.GetYear(dateTime),
                    ISOWeek.GetWeekOfYear(dateTime),
                    group.Count());
            })
            .OrderByDescending(week => week.Count)
            .ThenByDescending(week => week.Year)
            .ThenByDescending(week => week.Week)
            .First();

        return weeklyCounts.Count == 0 ? null : weeklyCounts;
    }
}
