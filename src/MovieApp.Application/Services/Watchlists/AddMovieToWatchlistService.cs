using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Watchlists;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Watchlists;

public sealed class AddMovieToWatchlistService(
    ICurrentUser currentUser,
    IWatchlistRepository watchlistRepository,
    IWatchlistItemRepository watchlistItemRepository,
    IMovieRepository movieRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator,
    IServiceScopeFactory? analyticsScopeFactory = null) : IAddMovieToWatchlistService
{
    public async Task<WatchlistItemMutationResult> AddAsync(
        Guid watchlistId,
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        if (await watchlistRepository.GetByIdForUserAsync(userId, watchlistId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested watchlist was not found.");
        }

        if (await watchlistItemRepository.ExistsForMovieAsync(watchlistId, movieId, cancellationToken))
        {
            return WatchlistItemMutationResult.AlreadyExists;
        }

        if (!await movieRepository.ExistsAsync(movieId, cancellationToken))
        {
            throw new NotFoundException("The requested movie was not found.");
        }

        var item = WatchlistItem.CreateForMovie(watchlistId, movieId, DateTime.UtcNow);
        var added = await watchlistItemRepository.TryAddAsync(item, cancellationToken);
        if (added)
        {
            await watchlistRepository.TouchAsync(watchlistId, DateTime.UtcNow, cancellationToken);
            await BackgroundAnalyticsInvalidation.RunAsync(
                analyticsCacheInvalidator,
                analyticsScopeFactory,
                userId,
                cancellationToken);
            return WatchlistItemMutationResult.Created;
        }

        return WatchlistItemMutationResult.AlreadyExists;
    }
}
