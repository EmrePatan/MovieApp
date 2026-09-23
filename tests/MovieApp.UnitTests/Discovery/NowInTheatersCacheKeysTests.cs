using MovieApp.Application.Caching;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Discovery;

public sealed class NowInTheatersCacheKeysTests
{
    [Fact]
    public void CreateSeparatesReleaseRegions()
    {
        var trKey = NowInTheatersCacheKeys.Create(new NowInTheatersCriteria("TR", 1, 20), ContentLocaleResolver.EnglishUnitedStates);
        var usKey = NowInTheatersCacheKeys.Create(new NowInTheatersCriteria("US", 1, 20), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEqual(trKey, usKey);
        Assert.Contains("TR", trKey);
        Assert.Contains("US", usKey);
    }

    [Fact]
    public void CreateSeparatesPagination()
    {
        var pageOne = NowInTheatersCacheKeys.Create(new NowInTheatersCriteria("TR", 1, 10), ContentLocaleResolver.EnglishUnitedStates);
        var pageTwo = NowInTheatersCacheKeys.Create(new NowInTheatersCriteria("TR", 2, 10), ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEqual(pageOne, pageTwo);
    }
}
