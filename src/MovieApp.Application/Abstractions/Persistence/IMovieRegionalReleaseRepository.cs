using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IMovieRegionalReleaseRepository
{
    Task<MovieRegionalRelease?> GetByMovieIdAndRegionAsync(
        Guid movieId,
        string region,
        CancellationToken cancellationToken = default);

    Task<MovieRegionalRelease> UpsertAsync(
        MovieRegionalRelease regionalRelease,
        CancellationToken cancellationToken = default);
}
