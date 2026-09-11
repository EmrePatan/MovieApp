using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Mapping;
using MovieApp.Application.Models.Watchlists;

namespace MovieApp.Application.Services.Watchlists;

public sealed class GetWatchlistService(
    ICurrentUser currentUser,
    IWatchlistRepository watchlistRepository,
    IWatchlistItemRepository watchlistItemRepository) : IGetWatchlistService
{
    public async Task<WatchlistDetailResult> GetAsync(
        Guid watchlistId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var watchlist = await watchlistRepository.GetByIdForUserAsync(userId, watchlistId, cancellationToken);
        if (watchlist is null)
        {
            throw new NotFoundException("The requested watchlist was not found.");
        }

        var items = await watchlistItemRepository.GetAllItemsAsync(watchlistId, cancellationToken);
        return WatchlistMapper.ToDetailResult(watchlist, items);
    }
}
