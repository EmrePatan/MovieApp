using MovieApp.Application.Caching;
using MovieApp.Application.Models.Search;

namespace MovieApp.UnitTests.Discovery;

public sealed class AdvancedDiscoverCacheKeysTests
{
    [Fact]
    public void CreateSeparatesWatchRegionProvidersAndMonetization()
    {
        var baseCriteria = new AdvancedDiscoverCriteria(
            SearchContentType.Movie,
            [],
            GenreMatchMode.All,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "KR",
            null,
            null,
            [],
            null,
            [],
            [],
            AdvancedDiscoverSort.PopularityDesc,
            1,
            20);

        var watchCriteria = baseCriteria with
        {
            WatchRegion = "TR",
            WatchProviderIds = [8, 337],
            WatchMonetizationTypes = [WatchMonetizationType.Stream],
        };

        var baseKey = AdvancedDiscoverCacheKeys.Create(baseCriteria);
        var watchKey = AdvancedDiscoverCacheKeys.Create(watchCriteria);

        Assert.NotEqual(baseKey, watchKey);
        Assert.Contains(":TR:", watchKey);
        Assert.Contains("8-337", watchKey);
        Assert.Contains("Stream", watchKey);
    }
}
