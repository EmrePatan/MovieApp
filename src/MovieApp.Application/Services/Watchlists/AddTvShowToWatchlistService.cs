using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Watchlists;

public sealed class AddTvShowToWatchlistService(
    ICurrentUser currentUser,
    IWatchlistRepository watchlistRepository,
    IWatchlistItemRepository watchlistItemRepository,
    ITvShowRepository tvShowRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator) : IAddTvShowToWatchlistService
{
    public async Task<WatchlistItemMutationResult> AddAsync(
        Guid watchlistId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        if (await watchlistRepository.GetByIdForUserAsync(userId, watchlistId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested watchlist was not found.");
        }

        if (await watchlistItemRepository.ExistsForTvShowAsync(watchlistId, tvShowId, cancellationToken))
        {
            return WatchlistItemMutationResult.AlreadyExists;
        }

        if (await tvShowRepository.GetByIdAsync(tvShowId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested TV show was not found.");
        }

        var item = WatchlistItem.CreateForTvShow(watchlistId, tvShowId, DateTime.UtcNow);
        var added = await watchlistItemRepository.TryAddAsync(item, cancellationToken);
        if (added)
        {
            await watchlistRepository.TouchAsync(watchlistId, DateTime.UtcNow, cancellationToken);
            await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
            return WatchlistItemMutationResult.Created;
        }

        return WatchlistItemMutationResult.AlreadyExists;
    }
}
