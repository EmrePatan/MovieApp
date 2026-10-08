using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.Watchlists;

public sealed class DeleteWatchlistService(
    ICurrentUser currentUser,
    IWatchlistRepository watchlistRepository,
    ICacheService? cacheService = null,
    IServiceScopeFactory? recommendationScopeFactory = null) : IDeleteWatchlistService
{
    public async Task DeleteAsync(Guid watchlistId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var deleted = await watchlistRepository.DeleteAsync(userId, watchlistId, cancellationToken);
        if (!deleted)
        {
            throw new NotFoundException("The requested watchlist was not found.");
        }

        // Deleting a list cascades its items, which are a recommendation input.
        if (cacheService is not null)
        {
            await BackgroundAnalyticsInvalidation.InvalidateRecommendationsAsync(
                cacheService,
                recommendationScopeFactory,
                userId,
                cancellationToken);
        }
    }
}
