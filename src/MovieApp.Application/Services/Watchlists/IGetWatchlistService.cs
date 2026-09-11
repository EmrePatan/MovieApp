using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public interface IGetWatchlistService
{
    Task<WatchlistDetailResult> GetAsync(Guid watchlistId, CancellationToken cancellationToken = default);
}
