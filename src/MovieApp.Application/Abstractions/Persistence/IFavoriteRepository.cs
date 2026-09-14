using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IFavoriteRepository
{
    Task<bool> ExistsForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetFavoritedMovieIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> movieIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> GetFavoritedTvShowIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> tvShowIds,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(Favorite favorite, CancellationToken cancellationToken = default);

    Task<bool> RemoveForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken = default);

    Task<bool> RemoveForTvShowAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Favorite> Favorites, int TotalCount)> GetUserFavoritesAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
