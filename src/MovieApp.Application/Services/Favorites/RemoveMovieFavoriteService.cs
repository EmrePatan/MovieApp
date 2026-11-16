using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Social;
using MovieApp.Application.Caching;
using MovieApp.Application.Identity;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Favorites;

public sealed class RemoveMovieFavoriteService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator,
    IServiceScopeFactory? analyticsScopeFactory = null,
    ISocialActivityRecorder? socialActivityRecorder = null) : IRemoveMovieFavoriteService
{
    public async Task RemoveAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await favoriteRepository.RemoveForMovieAsync(userId, movieId, cancellationToken);
        if (socialActivityRecorder is not null)
            await socialActivityRecorder.RetractAsync(userId, SocialActivityType.FavoritedMovie, movieId, cancellationToken);
        await BackgroundAnalyticsInvalidation.RunAsync(
            analyticsCacheInvalidator,
            analyticsScopeFactory,
            userId,
            cancellationToken);
    }
}
