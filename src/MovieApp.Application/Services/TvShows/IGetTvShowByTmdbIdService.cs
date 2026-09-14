using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.TvShows;

public interface IGetTvShowByTmdbIdService
{
    Task<TvShowDetailsResult> GetAsync(int tmdbId, CancellationToken cancellationToken = default);
}
