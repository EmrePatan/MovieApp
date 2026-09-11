using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public interface ICreateWatchlistService
{
    Task<WatchlistSummaryResult> CreateAsync(
        CreateWatchlistRequest request,
        CancellationToken cancellationToken = default);
}
