using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Favorites;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.Favorites;

public sealed class AddMovieFavoriteService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository,
    IMovieRepository movieRepository,
    IProfileStatisticsCache profileStatisticsCache) : IAddMovieFavoriteService
{
    public async Task<FavoriteMutationResult> AddAsync(Guid movieId, CancellationToken cancellationToken = default)
    {
        var userId = CurrentUserGuard.RequireUserId(currentUser);

        if (await favoriteRepository.ExistsForMovieAsync(userId, movieId, cancellationToken))
        {
            return FavoriteMutationResult.AlreadyExists;
        }

        if (await movieRepository.GetByIdAsync(movieId, cancellationToken) is null)
        {
            throw new NotFoundException("The requested movie was not found.");
        }

        var favorite = Favorite.CreateForMovie(userId, movieId, DateTime.UtcNow);

        var added = await favoriteRepository.TryAddAsync(favorite, cancellationToken);
        if (added)
        {
            await profileStatisticsCache.InvalidateForUserAsync(userId, cancellationToken);
            return FavoriteMutationResult.Created;
        }

        return FavoriteMutationResult.AlreadyExists;
    }
}
