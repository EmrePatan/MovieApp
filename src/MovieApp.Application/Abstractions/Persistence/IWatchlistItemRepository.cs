using MovieApp.Application.Models.Search;
using MovieApp.Application.Models.Watchlists;
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
        SearchContentType mediaType,
        WatchlistItemsSort sort,
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

    /// <summary>
    /// Poster paths of the most recently added items per list, newest first, skipping titles without a poster.
    /// Lists without posters are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetCoverPosterPathsByWatchlistIdsAsync(
        IReadOnlyCollection<Guid> watchlistIds,
        int perListLimit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingMovieAsync(
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetWatchlistIdsContainingTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WatchlistItem>> GetDistinctItemsForUserAsync(
        Guid userId,
        int maxItems,
        CancellationToken cancellationToken = default);
}
