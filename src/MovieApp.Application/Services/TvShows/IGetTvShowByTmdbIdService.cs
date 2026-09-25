using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.TvShows;

public interface IGetTvShowByTmdbIdService
{
    Task<TvShowDetailsResult> GetAsync(int tmdbId, CancellationToken cancellationToken = default);

    Task<TvShowDetailsResult> GetAsync(
        int tmdbId,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
