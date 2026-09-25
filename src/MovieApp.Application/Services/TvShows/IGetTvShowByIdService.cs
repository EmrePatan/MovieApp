using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Services.TvShows;

public interface IGetTvShowByIdService
{
    Task<TvShowDetailsResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TvShowDetailsResult> GetByIdAsync(
        Guid id,
        string contentLocale,
        CancellationToken cancellationToken = default);
}
