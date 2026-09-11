using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public interface IGetWatchlistsService
{
    Task<IReadOnlyList<WatchlistSummaryResult>> GetAsync(CancellationToken cancellationToken = default);
}
