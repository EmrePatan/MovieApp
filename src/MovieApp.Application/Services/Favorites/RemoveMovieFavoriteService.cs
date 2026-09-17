using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.Favorites;

public sealed class RemoveMovieFavoriteService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository,
    IUserAnalyticsCacheInvalidator analyticsCacheInvalidator) : IRemoveMovieFavoriteService
{
    public async Task RemoveAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await favoriteRepository.RemoveForMovieAsync(userId, movieId, cancellationToken);
        await analyticsCacheInvalidator.InvalidateForUserAsync(userId, cancellationToken);
    }
}
