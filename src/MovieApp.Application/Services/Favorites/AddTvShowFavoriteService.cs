using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Favorites;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Favorites;

public sealed class AddTvShowFavoriteService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository,
    ITvShowRepository tvShowRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator,
    IServiceScopeFactory? analyticsScopeFactory = null) : IAddTvShowFavoriteService
{
    public async Task<FavoriteMutationResult> AddAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        if (await favoriteRepository.ExistsForTvShowAsync(userId, tvShowId, cancellationToken))
        {
            return FavoriteMutationResult.AlreadyExists;
        }

        if (!await tvShowRepository.ExistsAsync(tvShowId, cancellationToken))
        {
            throw new NotFoundException("The requested TV show was not found.");
        }

        var favorite = Favorite.CreateForTvShow(userId, tvShowId, DateTime.UtcNow);

        var added = await favoriteRepository.TryAddAsync(favorite, cancellationToken);
        if (added)
        {
            await BackgroundAnalyticsInvalidation.RunAsync(
                analyticsCacheInvalidator,
                analyticsScopeFactory,
                userId,
                cancellationToken);
            return FavoriteMutationResult.Created;
        }

        return FavoriteMutationResult.AlreadyExists;
    }
}
