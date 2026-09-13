using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Identity;

namespace MovieApp.Application.Services.Favorites;

public sealed class GetFavoriteStatusService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository) : IGetFavoriteStatusService
{
    public async Task<bool> GetMovieStatusAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        return await favoriteRepository.ExistsForMovieAsync(userId, movieId, cancellationToken);
    }

    public async Task<bool> GetTvShowStatusAsync(Guid tvShowId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);
        return await favoriteRepository.ExistsForTvShowAsync(userId, tvShowId, cancellationToken);
    }
}
