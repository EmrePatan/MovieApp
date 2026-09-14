using MovieApp.Application.Models.WatchProviders;

namespace MovieApp.Application.Services.TvShows;

public interface IGetTvShowWatchProvidersService
{
    Task<WatchProvidersResult> GetWatchProvidersAsync(
        Guid tvShowId,
        string? region,
        CancellationToken cancellationToken = default);
}
