using MovieApp.Application.Caching;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Discovery;

public sealed class OnTvThisWeekCacheKeysTests
{
    [Fact]
    public void CreateSeparatesPagination()
    {
        var pageOne = OnTvThisWeekCacheKeys.Create(new OnTvThisWeekCriteria(1, 20), ContentLocaleResolver.EnglishUnitedStates);
        var pageTwo = OnTvThisWeekCacheKeys.Create(new OnTvThisWeekCriteria(2, 20), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEqual(pageOne, pageTwo);
        Assert.Contains("discovery-on-tv-this-week", pageOne);
    }

    [Fact]
    public void CreateSeparatesHomeRailAndDiscoverBrowseForSamePagination()
    {
        var discover = OnTvThisWeekCacheKeys.Create(
            new OnTvThisWeekCriteria(1, 10, OnTvThisWeekPresentationIntent.DiscoverBrowse),
            ContentLocaleResolver.EnglishUnitedStates);
        var home = OnTvThisWeekCacheKeys.Create(
            new OnTvThisWeekCriteria(1, 10, OnTvThisWeekPresentationIntent.HomeRail),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEqual(discover, home);
        Assert.Contains("DiscoverBrowse", discover);
        Assert.Contains("HomeRail", home);
    }
}
