using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Credits;

public sealed class TmdbCreditsMapperTests
{
    [Fact]
    public void MapsMovieCastInOrder()
    {
        var response = new TmdbCreditsResponseJson
        {
            Cast =
            [
                new TmdbCastMemberJson
                {
                    Id = 2,
                    Name = "Second",
                    Character = "B",
                    ProfilePath = "/b.jpg",
                    Order = 1,
                },
                new TmdbCastMemberJson
                {
                    Id = 1,
                    Name = "First",
                    Character = "A",
                    ProfilePath = "/a.jpg",
                    Order = 0,
                },
            ],
        };

        var result = TmdbCreditsMapper.ToCreditsResult(response);

        Assert.Equal(2, result.Cast.Count);
        Assert.Equal("Second", result.Cast[0].Name);
        Assert.Equal("First", result.Cast[1].Name);
    }

    [Fact]
    public void MapsAggregateTvCastCharacterFromRoles()
    {
        var response = new TmdbAggregateCreditsResponseJson
        {
            Cast =
            [
                new TmdbAggregateCastMemberJson
                {
                    Id = 10,
                    Name = "Actor",
                    ProfilePath = "/actor.jpg",
                    Order = 0,
                    Roles = [new TmdbAggregateRoleJson { Character = "Lead" }],
                },
            ],
        };

        var result = TmdbCreditsMapper.ToCreditsResult(response);

        Assert.Single(result.Cast);
        Assert.Equal("Lead", result.Cast[0].Character);
    }
}
