using MovieApp.Application.Caching;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Validation;
using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Discovery;

public sealed class WatchProviderBrowseFilterTests
{
    private static readonly WatchProviderFilter Filter = new("TR", [337, 8]);

    [Theory]
    [InlineData(DiscoverBrowseMode.Popular, true)]
    [InlineData(DiscoverBrowseMode.Trending, true)]
    [InlineData(DiscoverBrowseMode.NewReleases, true)]
    [InlineData(DiscoverBrowseMode.TopRated, false)]
    [InlineData(DiscoverBrowseMode.HiddenGems, false)]
    public void Browse_AllowsProviderFilterOnlyForProviderBackedLists(DiscoverBrowseMode mode, bool allowed)
    {
        var result = DiscoverBrowseValidator.Validate(Browse(mode, Filter));

        Assert.Equal(allowed, result.IsValid);
    }

    [Fact]
    public void Browse_RejectsProviderFilterWithoutRegion()
    {
        var result = DiscoverBrowseValidator.Validate(
            Browse(DiscoverBrowseMode.Popular, new WatchProviderFilter(string.Empty, [8])));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Browse_TreatsProviderFilterAsSupplemental_SoCatalogListsAreSkipped()
    {
        Assert.False(DiscoverBrowseValidator.HasSupplementalFilters(Browse(DiscoverBrowseMode.Trending, null)));
        Assert.True(DiscoverBrowseValidator.HasSupplementalFilters(Browse(DiscoverBrowseMode.Trending, Filter)));
    }

    [Fact]
    public void Browse_CacheKeySeparatesProviderSelections()
    {
        var unfiltered = DiscoveryBrowseCacheKeys.Create(Browse(DiscoverBrowseMode.Popular, null));
        var filtered = DiscoveryBrowseCacheKeys.Create(Browse(DiscoverBrowseMode.Popular, Filter));
        var other = DiscoveryBrowseCacheKeys.Create(
            Browse(DiscoverBrowseMode.Popular, new WatchProviderFilter("TR", [8])));

        Assert.Equal(3, new[] { unfiltered, filtered, other }.Distinct().Count());
    }

    [Fact]
    public void TmdbQueries_FilterBySubscriptionOnTheChosenProviders()
    {
        var criteria = new DiscoverProviderCriteria(
            DiscoverBrowseMode.Popular, 1, [], null, null, null, null, null, null, null, null, null, [], [], null)
        {
            WatchFilter = Filter
        };

        foreach (var query in new[]
                 {
                     TmdbDiscoverQueryBuilder.BuildMovieQuery(criteria),
                     TmdbDiscoverQueryBuilder.BuildTvQuery(criteria)
                 })
        {
            Assert.Contains("watch_region=TR", query);
            Assert.Contains("with_watch_providers=8|337", query);
            Assert.Contains("with_watch_monetization_types=flatrate", query);
        }
    }

    [Fact]
    public void WorldCinema_PassesProviderFilterToAdvancedDiscover()
    {
        var criteria = new WorldCinemaCriteria(SearchContentType.Movie, "KR", AdvancedDiscoverSort.PopularityDesc, 1, 20)
        {
            WatchFilter = Filter
        };

        var mapped = WorldCinemaService.ToAdvancedDiscoverCriteria(criteria);

        Assert.Equal("TR", mapped.WatchRegion);
        Assert.Equal([337, 8], mapped.WatchProviderIds);
        Assert.Equal([WatchMonetizationType.Stream], mapped.WatchMonetizationTypes);
        Assert.NotEqual(
            WorldCinemaCacheKeys.Create(criteria with { WatchFilter = null }, "en-US"),
            WorldCinemaCacheKeys.Create(criteria, "en-US"));
    }

    private static DiscoverBrowseCriteria Browse(DiscoverBrowseMode mode, WatchProviderFilter? filter) =>
        new(mode, SearchContentType.Movie, [], null, null, null, null, null, null, null, null, null, [], [], null, 1, 20)
        {
            WatchFilter = filter
        };
}
