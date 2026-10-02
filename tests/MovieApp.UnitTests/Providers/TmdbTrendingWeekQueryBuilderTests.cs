using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Providers;

public sealed class TmdbTrendingWeekQueryBuilderTests
{
    [Fact]
    public void BuildQueryUsesRequestedPage()
    {
        Assert.Equal("page=3", TmdbTrendingWeekQueryBuilder.BuildQuery(3));
        Assert.Equal("page=1", TmdbTrendingWeekQueryBuilder.BuildQuery(0));
    }
}
