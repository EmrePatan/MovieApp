using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.WatchProviders;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeWatchProviderService : IWatchProviderService
{
    public Task<WatchProvidersResult> GetMovieWatchProvidersAsync(
        int tmdbId,
        string region,
        CancellationToken cancellationToken = default)
    {
        if (tmdbId != FakeMovieDataProvider.InterstellarTmdbId)
        {
            return Task.FromResult(new WatchProvidersResult(region, [], null));
        }

        return Task.FromResult(new WatchProvidersResult(
            region,
            [
                new WatchProviderResult(
                    8,
                    "Netflix",
                    "/fake/netflix.png",
                    1,
                    [WatchProviderAvailabilityType.Flatrate, WatchProviderAvailabilityType.Rent],
                    "https://www.themoviedb.org/movie/157336/watch"),
                new WatchProviderResult(
                    337,
                    "Disney Plus",
                    "/fake/disney.png",
                    2,
                    [WatchProviderAvailabilityType.Flatrate],
                    "https://www.themoviedb.org/movie/157336/watch")
            ],
            "https://www.themoviedb.org/movie/157336/watch"));
    }

    public Task<WatchProvidersResult> GetTvShowWatchProvidersAsync(
        int tmdbId,
        string region,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WatchProvidersResult(region, [], null));
}
