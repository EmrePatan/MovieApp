using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Services.Movies;

public interface IGetMovieByTmdbIdService
{
    Task<MovieDetailsResult> GetAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<MovieDetailsResult> GetAsync(
        int tmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
