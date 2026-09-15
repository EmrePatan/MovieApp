using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.MovieFollows;

public static class MovieFollowReleaseDateResolver
{
    public static DateOnly? Resolve(MovieRegionalRelease? regionalRelease, DateOnly? globalReleaseDate)
    {
        if (regionalRelease is not null)
        {
            return regionalRelease.EffectiveReleaseDate;
        }

        return globalReleaseDate;
    }
}
