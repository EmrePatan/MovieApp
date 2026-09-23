using MovieApp.Application.Caching;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Discovery;

public sealed class WorldCinemaCacheKeysTests
{
    [Fact]
    public void CreateSeparatesMediaTypeOriginCountryAndSort()
    {
        var movieKr = WorldCinemaCacheKeys.Create(
            new WorldCinemaCriteria(SearchContentType.Movie, "KR", AdvancedDiscoverSort.PopularityDesc, 1, 20),
            ContentLocaleResolver.EnglishUnitedStates);
        var tvKr = WorldCinemaCacheKeys.Create(
            new WorldCinemaCriteria(SearchContentType.Tv, "KR", AdvancedDiscoverSort.PopularityDesc, 1, 20),
            ContentLocaleResolver.EnglishUnitedStates);
        var movieJp = WorldCinemaCacheKeys.Create(
            new WorldCinemaCriteria(SearchContentType.Movie, "JP", AdvancedDiscoverSort.PopularityDesc, 1, 20),
            ContentLocaleResolver.EnglishUnitedStates);
        var movieKrRated = WorldCinemaCacheKeys.Create(
            new WorldCinemaCriteria(SearchContentType.Movie, "KR", AdvancedDiscoverSort.RatingDesc, 1, 20),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEqual(movieKr, tvKr);
        Assert.NotEqual(movieKr, movieJp);
        Assert.NotEqual(movieKr, movieKrRated);
        Assert.Contains("discovery-world-cinema", movieKr);
        Assert.Contains("KR", movieKr);
        Assert.EndsWith(":v3", movieKr);
    }
}
