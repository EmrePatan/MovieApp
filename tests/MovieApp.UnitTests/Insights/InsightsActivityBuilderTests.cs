using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsActivityBuilderTests
{
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

    [Fact]
    public void BuildReturnsRolling364DayWindow()
    {
        var utcNow = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        var raw = CreateRaw(
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            []);

        var result = InsightsActivityBuilder.Build(raw, Istanbul, utcNow);

        Assert.Equal(364, result.Days.Count);
        Assert.Equal(new DateOnly(2025, 9, 19), result.Days[0].Date);
        Assert.Equal(new DateOnly(2026, 9, 17), result.Days[^1].Date);
    }

    [Fact]
    public void BuildMarksDaysBeforeMembershipAsBeforeJoin()
    {
        var utcNow = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        var memberSince = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        var raw = CreateRaw(memberSince, []);

        var result = InsightsActivityBuilder.Build(raw, Istanbul, utcNow);

        Assert.All(
            result.Days.Where(day => day.Date < new DateOnly(2026, 9, 10)),
            day => Assert.Equal(InsightsActivityDayState.BeforeJoin, day.State));
    }

    [Fact]
    public void BuildAggregatesMoviesAndEpisodesOnSameLocalDay()
    {
        var utcNow = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        var memberSince = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var raw = CreateRaw(
            memberSince,
            [
                new InsightsActivityEventData(new DateTime(2026, 9, 16, 21, 0, 0, DateTimeKind.Utc), true, null),
                new InsightsActivityEventData(new DateTime(2026, 9, 16, 22, 0, 0, DateTimeKind.Utc), false, null),
            ]);

        var day = InsightsActivityBuilder.Build(raw, Istanbul, utcNow)
            .Days.Single(item => item.Date == new DateOnly(2026, 9, 17));

        Assert.Equal(InsightsActivityDayState.Active, day.State);
        Assert.Equal(1, day.Movies);
        Assert.Equal(1, day.Episodes);
        Assert.Equal(2, day.Total);
    }

    [Fact]
    public void BuildUsesWindowLevelIntensityBuckets()
    {
        var utcNow = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);
        var memberSince = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var raw = CreateRaw(
            memberSince,
            [
                new InsightsActivityEventData(new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc), true, null),
                new InsightsActivityEventData(new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc), true, null),
                new InsightsActivityEventData(new DateTime(2026, 9, 15, 13, 0, 0, DateTimeKind.Utc), true, null),
                new InsightsActivityEventData(new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc), true, null),
                new InsightsActivityEventData(new DateTime(2026, 9, 15, 15, 0, 0, DateTimeKind.Utc), true, null),
            ]);

        var result = InsightsActivityBuilder.Build(raw, Istanbul, utcNow);
        var highDay = result.Days.Single(day => day.Date == new DateOnly(2026, 9, 15));
        var lowDay = result.Days.Single(day => day.Date == new DateOnly(2026, 9, 16));

        Assert.Equal(4, highDay.IntensityBucket);
        Assert.Equal(1, lowDay.IntensityBucket);
    }

    [Fact]
    public void CalculateMostActiveWeekdayReturnsNullForInsufficientSample()
    {
        var activeDays = new[]
        {
            CreateDay(DayOfWeek.Monday, 3),
            CreateDay(DayOfWeek.Tuesday, 2),
            CreateDay(DayOfWeek.Wednesday, 2),
            CreateDay(DayOfWeek.Thursday, 2),
        };

        Assert.Null(InsightsActivityBuilder.CalculateMostActiveWeekday(activeDays));
    }

    [Fact]
    public void CalculateMostActiveWeekdayReturnsWinnerWhenThresholdMet()
    {
        var activeDays = Enumerable.Range(0, 7)
            .SelectMany(offset => Enumerable.Range(0, 2)
                .Select(_ => CreateDay(DayOfWeek.Monday, 1)))
            .Concat(Enumerable.Range(0, 3).Select(_ => CreateDay(DayOfWeek.Tuesday, 1)))
            .ToList();

        Assert.Equal(DayOfWeek.Monday, InsightsActivityBuilder.CalculateMostActiveWeekday(activeDays));
    }

    [Fact]
    public void CalculateLongestStreakCountsConsecutiveLocalDates()
    {
        var dates = new[]
        {
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 2),
            new DateOnly(2026, 9, 3),
            new DateOnly(2026, 9, 5),
        };

        Assert.Equal(3, InsightsActivityBuilder.CalculateLongestStreak(dates));
    }

    private static InsightsAnalyticsRawData CreateRaw(
        DateTime memberSinceUtc,
        IReadOnlyList<InsightsActivityEventData> events) =>
        new(
            memberSinceUtc,
            0,
            0,
            0,
            0,
            events,
            [],
            [],
            0,
            0,
            0,
            0,
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    private static InsightsActivityDayResult CreateDay(DayOfWeek weekday, int total)
    {
        var date = DateOnly.FromDateTime(new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));
        while (date.DayOfWeek != weekday)
        {
            date = date.AddDays(1);
        }

        return new InsightsActivityDayResult(date, total, 0, total, InsightsActivityDayState.Active, 1);
    }
}
