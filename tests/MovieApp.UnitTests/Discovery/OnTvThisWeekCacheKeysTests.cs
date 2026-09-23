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
}
