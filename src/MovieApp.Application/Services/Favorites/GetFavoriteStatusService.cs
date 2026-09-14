using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Identity;
using MovieApp.Application.Models.Favorites;

namespace MovieApp.Application.Services.Favorites;

public sealed class GetFavoriteStatusService(
    ICurrentUser currentUser,
    IFavoriteRepository favoriteRepository) : IGetFavoriteStatusService
{
    private const int MaxBatchItems = 20;

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

    public async Task<IReadOnlyList<FavoriteStatusLookupResult>> GetBatchStatusAsync(
        IReadOnlyList<FavoriteContentReference> items,
        CancellationToken cancellationToken = default)
    {
        if (items.Count > MaxBatchItems)
        {
            throw new ValidationException($"A maximum of {MaxBatchItems} favorite status items is allowed.");
        }

        var userId = CurrentUserGuard.RequireUserId(currentUser);

        var movieIds = items
            .Where(item => string.Equals(item.ContentType, "movie", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .Distinct()
            .ToList();

        var tvShowIds = items
            .Where(item => string.Equals(item.ContentType, "tv", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .Distinct()
            .ToList();

        var favoritedMovieIds = await favoriteRepository.GetFavoritedMovieIdsAsync(
            userId,
            movieIds,
            cancellationToken);

        var favoritedTvShowIds = await favoriteRepository.GetFavoritedTvShowIdsAsync(
            userId,
            tvShowIds,
            cancellationToken);

        return items
            .Select(item =>
            {
                var isFavorited = string.Equals(item.ContentType, "movie", StringComparison.OrdinalIgnoreCase)
                    ? favoritedMovieIds.Contains(item.Id)
                    : string.Equals(item.ContentType, "tv", StringComparison.OrdinalIgnoreCase)
                        ? favoritedTvShowIds.Contains(item.Id)
                        : false;

                return new FavoriteStatusLookupResult(item.ContentType, item.Id, isFavorited);
            })
            .ToList();
    }
}
