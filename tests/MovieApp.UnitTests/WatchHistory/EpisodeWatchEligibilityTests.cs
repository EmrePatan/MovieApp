using MovieApp.Application.Services.WatchHistory;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class EpisodeWatchEligibilityTests
{
    private static readonly DateOnly Today = new(2026, 10, 6);

    [Fact]
    public void IsCurrentlyWatchable_ReturnsTrueForPastAirDate()
    {
        Assert.True(EpisodeWatchEligibility.IsCurrentlyWatchable(Today.AddDays(-1), Today));
    }

    [Fact]
    public void IsCurrentlyWatchable_ReturnsTrueWhenAirDateEqualsToday()
    {
        Assert.True(EpisodeWatchEligibility.IsCurrentlyWatchable(Today, Today));
    }

    [Fact]
    public void IsCurrentlyWatchable_ReturnsFalseForFutureAirDate()
    {
        Assert.False(EpisodeWatchEligibility.IsCurrentlyWatchable(Today.AddDays(1), Today));
    }

    [Fact]
    public void IsCurrentlyWatchable_ReturnsFalseForNullAirDate()
    {
        Assert.False(EpisodeWatchEligibility.IsCurrentlyWatchable(null, Today));
    }

    [Fact]
    public void IsCurrentlyWatchable_BecomesEligibleWhenTodayReachesAirDateWithoutDataChange()
    {
        var airDate = new DateOnly(2026, 10, 7);

        Assert.False(EpisodeWatchEligibility.IsCurrentlyWatchable(airDate, Today));
        Assert.True(EpisodeWatchEligibility.IsCurrentlyWatchable(airDate, airDate));
    }
}
