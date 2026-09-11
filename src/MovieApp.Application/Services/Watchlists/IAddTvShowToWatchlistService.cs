using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public interface IAddTvShowToWatchlistService
{
    Task<WatchlistItemMutationResult> AddAsync(
        Guid watchlistId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);
}
