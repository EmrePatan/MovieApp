using MovieApp.Infrastructure.Providers.Tmdb;

namespace MovieApp.UnitTests.Discovery;

public sealed class TmdbOnTheAirQueryBuilderTests
{
    [Fact]
    public void BuildQueryMapsPageOnly()
    {
        var query = TmdbOnTheAirQueryBuilder.BuildQuery(2);

        Assert.Equal("page=2", query);
        Assert.DoesNotContain("air_date", query);
        Assert.DoesNotContain("first_air_date", query);
    }
}
