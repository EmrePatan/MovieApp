using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Credits;

public sealed class TmdbCreditsMapperTests
{
    [Fact]
    public void MapsMovieCastInProviderOrder()
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
    public void MapsMovieCrewWithJobs()
    {
        var response = new TmdbCreditsResponseJson
        {
            Crew =
            [
                new TmdbCrewMemberJson
                {
                    Id = 10,
                    Name = "Director",
                    Department = "Directing",
                    Job = "Director",
                    ProfilePath = "/director.jpg",
                },
            ],
        };

        var result = TmdbCreditsMapper.ToCreditsResult(response);

        Assert.Single(result.Crew);
        Assert.Equal("Director", result.Crew[0].Name);
        Assert.Equal("Directing", result.Crew[0].Department);
        Assert.Equal(["Director"], result.Crew[0].Jobs);
        Assert.Equal("/director.jpg", result.Crew[0].ProfileImagePath);
    }

    [Fact]
    public void MapsMovieCastWithMissingCharacterAndProfile()
    {
        var response = new TmdbCreditsResponseJson
        {
            Cast =
            [
                new TmdbCastMemberJson
                {
                    Id = 1,
                    Name = "Unknown Role",
                    Order = 0,
                },
            ],
        };

        var result = TmdbCreditsMapper.ToCreditsResult(response);

        Assert.Single(result.Cast);
        Assert.Null(result.Cast[0].Character);
        Assert.Null(result.Cast[0].ProfileImagePath);
    }

    [Fact]
    public void MapsAggregateTvCastRolesAndEpisodeCount()
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
                    TotalEpisodeCount = 12,
                    Roles =
                    [
                        new TmdbAggregateRoleJson { Character = "Lead", EpisodeCount = 10 },
                        new TmdbAggregateRoleJson { Character = "Guest", EpisodeCount = 2 },
                    ],
                },
            ],
        };

        var result = TmdbCreditsMapper.ToCreditsResult(response);

        Assert.Single(result.Cast);
        Assert.Equal("Lead", result.Cast[0].Character);
        Assert.Equal(12, result.Cast[0].TotalEpisodeCount);
        Assert.Equal(2, result.Cast[0].Roles!.Count);
        Assert.Equal("Lead", result.Cast[0].Roles![0].Character);
        Assert.Equal(10, result.Cast[0].Roles![0].EpisodeCount);
    }

    [Fact]
    public void MapsAggregateTvCrewWithMultipleJobs()
    {
        var response = new TmdbAggregateCreditsResponseJson
        {
            Crew =
            [
                new TmdbAggregateCrewMemberJson
                {
                    Id = 20,
                    Name = "Creator",
                    Department = "Production",
                    ProfilePath = "/creator.jpg",
                    Jobs =
                    [
                        new TmdbAggregateCrewJobJson { Job = "Executive Producer", EpisodeCount = 62 },
                        new TmdbAggregateCrewJobJson { Job = "Writer", EpisodeCount = 5 },
                    ],
                },
            ],
        };

        var result = TmdbCreditsMapper.ToCreditsResult(response);

        Assert.Single(result.Crew);
        Assert.Equal(["Executive Producer", "Writer"], result.Crew[0].Jobs);
    }
}
