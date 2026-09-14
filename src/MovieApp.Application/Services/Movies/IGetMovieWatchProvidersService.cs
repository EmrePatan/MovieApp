using MovieApp.Application.Models.WatchProviders;

namespace MovieApp.Application.Services.Movies;

public interface IGetMovieWatchProvidersService
{
    Task<WatchProvidersResult> GetWatchProvidersAsync(
        Guid movieId,
        string? region,
        CancellationToken cancellationToken = default);
}
