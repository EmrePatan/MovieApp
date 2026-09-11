using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Services.Movies;

public interface IGetMovieByIdService
{
    Task<MovieDetailsResult> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
