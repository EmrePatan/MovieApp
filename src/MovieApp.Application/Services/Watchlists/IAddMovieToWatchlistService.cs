using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public interface IAddMovieToWatchlistService
{
    Task<WatchlistItemMutationResult> AddAsync(
        Guid watchlistId,
        Guid movieId,
        CancellationToken cancellationToken = default);
}
