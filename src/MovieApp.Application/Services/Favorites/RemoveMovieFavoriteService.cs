using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.Favorites;

public sealed class RemoveMovieFavoriteService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository) : IRemoveMovieFavoriteService
{
    public async Task RemoveAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await favoriteRepository.RemoveForMovieAsync(userId, movieId, cancellationToken);
    }
}
