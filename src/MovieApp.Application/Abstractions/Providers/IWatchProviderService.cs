using MovieApp.Application.Models.WatchProviders;

namespace MovieApp.Application.Abstractions.Providers;

public interface IWatchProviderService
{
    Task<WatchProvidersResult> GetMovieWatchProvidersAsync(
        int tmdbId,
        string region,
        CancellationToken cancellationToken = default);

    Task<WatchProvidersResult> GetTvShowWatchProvidersAsync(
        int tmdbId,
        string region,
        CancellationToken cancellationToken = default);
}
