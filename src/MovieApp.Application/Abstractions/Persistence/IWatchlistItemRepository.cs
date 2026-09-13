using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IWatchlistItemRepository
{
    Task<bool> ExistsForMovieAsync(
        Guid watchlistId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForTvShowAsync(
        Guid watchlistId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(WatchlistItem item, CancellationToken cancellationToken = default);

    Task<bool> RemoveForMovieAsync(
        Guid watchlistId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveForTvShowAsync(
        Guid watchlistId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WatchlistItem> Items, int TotalCount)> GetItemsAsync(
        Guid watchlistId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WatchlistItem>> GetAllItemsAsync(
        Guid watchlistId,
        CancellationToken cancellationToken = default);

    Task<int> GetItemCountAsync(Guid watchlistId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, int>> GetItemCountsByWatchlistIdsAsync(
        IReadOnlyCollection<Guid> watchlistIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);
}
