using MovieApp.Application.Caching;
using MovieApp.Application.Models.Discovery;

namespace MovieApp.UnitTests.Discovery;

public sealed class NowInTheatersCacheKeysTests
{
    [Fact]
    public void CreateSeparatesReleaseRegions()
    {
        var trKey = NowInTheatersCacheKeys.Create(new NowInTheatersCriteria("TR", 1, 20));
        var usKey = NowInTheatersCacheKeys.Create(new NowInTheatersCriteria("US", 1, 20));

        Assert.NotEqual(trKey, usKey);
        Assert.Contains("TR", trKey);
        Assert.Contains("US", usKey);
    }

    [Fact]
    public void CreateSeparatesPagination()
    {
        var pageOne = NowInTheatersCacheKeys.Create(new NowInTheatersCriteria("TR", 1, 10));
        var pageTwo = NowInTheatersCacheKeys.Create(new NowInTheatersCriteria("TR", 2, 10));

        Assert.NotEqual(pageOne, pageTwo);
    }
}
