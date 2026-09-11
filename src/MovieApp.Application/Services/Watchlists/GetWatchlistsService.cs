using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public sealed class GetWatchlistsService(
    ICurrentUser currentUser,
    IWatchlistRepository watchlistRepository,
    IWatchlistItemRepository watchlistItemRepository) : IGetWatchlistsService
{
    public async Task<IReadOnlyList<WatchlistSummaryResult>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        var watchlists = await watchlistRepository.GetUserWatchlistsAsync(userId, cancellationToken);
        var itemCounts = await watchlistItemRepository.GetItemCountsByWatchlistIdsAsync(
            watchlists.Select(watchlist => watchlist.Id).ToArray(),
            cancellationToken);

        return watchlists
            .Select(watchlist => WatchlistMapper.ToSummaryResult(
                watchlist,
                itemCounts.GetValueOrDefault(watchlist.Id)))
            .ToList();
    }
}
