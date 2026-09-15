using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.UnitTests.Providers;

public sealed class FakeDiscoverProviderTests
{
    [Fact]
    public async Task DiscoverMoviesAsyncReturnsFilteredCatalog()
    {
        var provider = new FakeMovieDataProvider(new MovieDataProviderCallTracker());

        var result = await provider.DiscoverMoviesAsync(
            new DiscoverProviderCriteria(
                DiscoverBrowseMode.TopRated,
                1,
                [],
                null,
                8.0m,
                null,
                null),
            CancellationToken.None);

        Assert.Single(result.Results);
        Assert.Equal("Discover Movie Alpha", result.Results[0].Title);
    }

    [Fact]
    public async Task DiscoverTvShowsAsyncReturnsFilteredCatalog()
    {
        var provider = new FakeTvShowDataProvider(new TvShowDataProviderCallTracker());

        var result = await provider.DiscoverTvShowsAsync(
            new DiscoverProviderCriteria(
                DiscoverBrowseMode.Trending,
                1,
                [],
                null,
                null,
                "en",
                null),
            CancellationToken.None);

        Assert.Single(result.Results);
        Assert.Equal("Discover TV Alpha", result.Results[0].Title);
    }
}
