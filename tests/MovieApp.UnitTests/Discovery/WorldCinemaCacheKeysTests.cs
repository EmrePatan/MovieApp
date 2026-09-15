using MovieApp.Application.Caching;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Search;

namespace MovieApp.UnitTests.Discovery;

public sealed class WorldCinemaCacheKeysTests
{
    [Fact]
    public void CreateSeparatesMediaTypeOriginCountryAndSort()
    {
        var movieKr = WorldCinemaCacheKeys.Create(
            new WorldCinemaCriteria(SearchContentType.Movie, "KR", AdvancedDiscoverSort.PopularityDesc, 1, 20));
        var tvKr = WorldCinemaCacheKeys.Create(
            new WorldCinemaCriteria(SearchContentType.Tv, "KR", AdvancedDiscoverSort.PopularityDesc, 1, 20));
        var movieJp = WorldCinemaCacheKeys.Create(
            new WorldCinemaCriteria(SearchContentType.Movie, "JP", AdvancedDiscoverSort.PopularityDesc, 1, 20));
        var movieKrRated = WorldCinemaCacheKeys.Create(
            new WorldCinemaCriteria(SearchContentType.Movie, "KR", AdvancedDiscoverSort.RatingDesc, 1, 20));

        Assert.NotEqual(movieKr, tvKr);
        Assert.NotEqual(movieKr, movieJp);
        Assert.NotEqual(movieKr, movieKrRated);
        Assert.Contains("discovery-world-cinema", movieKr);
        Assert.Contains("KR", movieKr);
    }
}
