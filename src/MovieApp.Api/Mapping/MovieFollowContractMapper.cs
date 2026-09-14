using MovieApp.Application.Models.MovieFollows;
using MovieApp.Contracts.MovieFollows;

namespace MovieApp.Api.Mapping;

public static class MovieFollowContractMapper
{
    public static MovieFollowStatusResponse ToStatusResponse(MovieFollowStatusResult result) =>
        new(result.IsFollowing);
}
