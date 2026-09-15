using MovieApp.Application.Caching;
using MovieApp.Application.Models.Search;

namespace MovieApp.UnitTests.Caching;

public sealed class DiscoveryBrowseCacheKeysTests
{
    [Fact]
    public void CreateIncludesModeTypePaginationAndFilters()
    {
        var genreId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var criteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.TopRated,
            SearchContentType.All,
            [genreId],
            2024,
            7.5m,
            "en",
            DiscoverBrowseSort.RatingDesc,
            2,
            10);

        var key = DiscoveryBrowseCacheKeys.Create(criteria);

        Assert.StartsWith(DiscoveryBrowseCacheKeys.Prefix, key, StringComparison.Ordinal);
        Assert.Contains("TopRated", key, StringComparison.Ordinal);
        Assert.Contains("All", key, StringComparison.Ordinal);
        Assert.Contains(":2:10:", key, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateProducesDistinctKeysForDifferentCriteria()
    {
        var baseCriteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.Trending,
            SearchContentType.Movie,
            [],
            null,
            null,
            null,
            null,
            1,
            20);

        var otherCriteria = baseCriteria with { Mode = DiscoverBrowseMode.NewReleases };

        Assert.NotEqual(
            DiscoveryBrowseCacheKeys.Create(baseCriteria),
            DiscoveryBrowseCacheKeys.Create(otherCriteria));
    }
}
