using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;

namespace MovieApp.UnitTests.Home;

public sealed class TrendingWeekReleaseEligibilityTests
{
    private static readonly DateOnly Today = new(2026, 10, 4);

    [Fact]
    public void IsReleasedTreatsMissingDateAsEligible()
    {
        Assert.True(TrendingWeekReleaseEligibility.IsReleased(null, Today));
    }

    [Fact]
    public void IsReleasedIncludesMovieReleasedToday()
    {
        Assert.True(TrendingWeekReleaseEligibility.IsReleased(Today, Today));
    }

    [Fact]
    public void IsReleasedIncludesPastMovieRelease()
    {
        Assert.True(TrendingWeekReleaseEligibility.IsReleased(Today.AddDays(-30), Today));
    }

    [Fact]
    public void IsReleasedExcludesFutureMovieRelease()
    {
        Assert.False(TrendingWeekReleaseEligibility.IsReleased(Today.AddDays(1), Today));
    }

    [Fact]
    public void IsReleasedIncludesTvFirstAirToday()
    {
        Assert.True(TrendingWeekReleaseEligibility.IsReleased(Today, Today));
    }

    [Fact]
    public void IsReleasedExcludesFutureTvFirstAir()
    {
        Assert.False(TrendingWeekReleaseEligibility.IsReleased(Today.AddDays(14), Today));
    }

    [Fact]
    public void FilterReleasedPreservesProviderOrderAmongEligibleTitles()
    {
        var items = new List<SearchItem>
        {
            Item("Past One", Today.AddDays(-1)),
            Item("Future", Today.AddDays(30)),
            Item("Today Title", Today),
            Item("Past Two", Today.AddDays(-10)),
        };

        var filtered = TrendingWeekReleaseEligibility.FilterReleased(items, Today);

        Assert.Equal(["Past One", "Today Title", "Past Two"], filtered.Select(item => item.Title).ToArray());
    }

    [Fact]
    public void FilterReleasedProviderItemsExcludesFutureTitles()
    {
        var items = new[]
        {
            ProviderItem("movie", 1, Today.AddDays(-1)),
            ProviderItem("tv", 2, Today.AddDays(5)),
        };

        var filtered = TrendingWeekReleaseEligibility.FilterReleased(items, Today);

        Assert.Single(filtered);
        Assert.Equal(1, filtered[0].TmdbId);
    }

    [Fact]
    public void IsReleasedBecomesEligibleWhenTodayReachesReleaseDate()
    {
        var releaseDate = new DateOnly(2026, 12, 15);
        Assert.False(TrendingWeekReleaseEligibility.IsReleased(releaseDate, new DateOnly(2026, 12, 14)));
        Assert.True(TrendingWeekReleaseEligibility.IsReleased(releaseDate, releaseDate));
    }

    private static SearchItem Item(string title, DateOnly? releaseDate) =>
        new(
            Guid.NewGuid(),
            "movie",
            title,
            null,
            null,
            "/poster.jpg",
            null,
            releaseDate,
            7m,
            100,
            releaseDate?.Year);

    private static TrendingWeekProviderItem ProviderItem(string mediaType, int tmdbId, DateOnly releaseDate) =>
        new(
            mediaType,
            tmdbId,
            $"Title {tmdbId}",
            null,
            null,
            releaseDate,
            "/poster.jpg",
            null,
            7m,
            100);
}
