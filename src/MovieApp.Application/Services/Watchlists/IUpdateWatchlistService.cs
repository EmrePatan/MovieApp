using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public interface IUpdateWatchlistService
{
    Task<WatchlistSummaryResult> UpdateAsync(
        Guid watchlistId,
        UpdateWatchlistRequest request,
        CancellationToken cancellationToken = default);
}
