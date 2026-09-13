using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.Watchlists;

public sealed class RemoveTvShowFromWatchlistService(
    ICurrentUser currentUser,
    IWatchlistRepository watchlistRepository,
    IWatchlistItemRepository watchlistItemRepository,
    IProfileStatisticsCache profileStatisticsCache) : IRemoveTvShowFromWatchlistService
{
    public async Task RemoveAsync(
        Guid watchlistId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        if (await watchlistRepository.GetByIdForUserAsync(userId, watchlistId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested watchlist was not found.");
        }

        await watchlistItemRepository.RemoveForTvShowAsync(watchlistId, tvShowId, cancellationToken);
        await watchlistRepository.TouchAsync(watchlistId, DateTime.UtcNow, cancellationToken);
        await profileStatisticsCache.InvalidateForUserAsync(userId, cancellationToken);
    }
}
