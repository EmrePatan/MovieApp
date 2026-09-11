using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public interface IGetWatchlistItemsService
{
    Task<WatchlistItemsResult> GetAsync(
        Guid watchlistId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
