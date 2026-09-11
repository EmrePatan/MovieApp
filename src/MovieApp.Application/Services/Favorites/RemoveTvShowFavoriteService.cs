using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.Favorites;

public sealed class RemoveTvShowFavoriteService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository) : IRemoveTvShowFavoriteService
{
    public async Task RemoveAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        await favoriteRepository.RemoveForTvShowAsync(userId, tvShowId, cancellationToken);
    }
}
