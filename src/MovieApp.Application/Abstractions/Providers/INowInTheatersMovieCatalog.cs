using MovieApp.Application.Models.Providers;

namespace MovieApp.Application.Abstractions.Providers;

public interface INowInTheatersMovieCatalog
{
    Task<MovieProviderSearchResult> GetNowPlayingMoviesAsync(
        string releaseRegion,
        int page,
        CancellationToken cancellationToken = default);
}
