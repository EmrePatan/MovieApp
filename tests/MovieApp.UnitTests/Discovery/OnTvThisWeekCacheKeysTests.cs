using MovieApp.Application.Caching;
using MovieApp.Application.Models.Discovery;

namespace MovieApp.UnitTests.Discovery;

public sealed class OnTvThisWeekCacheKeysTests
{
    [Fact]
    public void CreateSeparatesPagination()
    {
        var pageOne = OnTvThisWeekCacheKeys.Create(new OnTvThisWeekCriteria(1, 20));
        var pageTwo = OnTvThisWeekCacheKeys.Create(new OnTvThisWeekCriteria(2, 20));

        Assert.NotEqual(pageOne, pageTwo);
        Assert.Contains("discovery-on-tv-this-week", pageOne);
    }
}
