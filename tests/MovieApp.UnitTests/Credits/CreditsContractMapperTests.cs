using MovieApp.Api.Mapping;
using MovieApp.Application.Models.Credits;

namespace MovieApp.UnitTests.Credits;

public sealed class CreditsContractMapperTests
{
    [Fact]
    public void ToResponseMapsCastAndCrew()
    {
        var result = new CreditsResult(
            [
                new(1, "Actor", "Lead", "/actor.jpg", 0, 10, [new CastRoleResult("Lead", 10)]),
            ],
            [
                new(2, "Director", "Directing", ["Director"], "/director.jpg"),
            ]);

        var response = CreditsContractMapper.ToResponse(result);

        Assert.Single(response.Cast);
        Assert.Single(response.Crew);
        Assert.Equal("Lead", response.Cast[0].Character);
        Assert.Equal(10, response.Cast[0].TotalEpisodeCount);
        Assert.NotNull(response.Cast[0].Roles);
        Assert.Equal(["Director"], response.Crew[0].Jobs);
    }
}
