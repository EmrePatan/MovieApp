using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Discovery;

public sealed class TmdbNowPlayingQueryBuilderTests
{
    [Fact]
    public void BuildQueryMapsReleaseRegionAndPage()
    {
        var query = TmdbNowPlayingQueryBuilder.BuildQuery("tr", 2);

        Assert.Equal("region=TR&page=2", query);
    }

    [Fact]
    public void BuildQueryUsesUppercaseRegionCode()
    {
        var query = TmdbNowPlayingQueryBuilder.BuildQuery("gb", 1);

        Assert.Contains("region=GB", query);
    }
}
