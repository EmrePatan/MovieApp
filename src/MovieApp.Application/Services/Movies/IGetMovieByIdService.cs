using MovieApp.Application.Models.Movies;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Movies;

public interface IGetMovieByIdService
{
    Task<MovieDetailsResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<MovieDetailsResult> GetByIdAsync(
        Guid id,
        Movie? prefetchedMovie,
        CancellationToken cancellationToken = default);
}
