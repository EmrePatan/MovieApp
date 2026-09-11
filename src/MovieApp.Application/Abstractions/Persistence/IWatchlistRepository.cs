using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IWatchlistRepository
{
    Task<Watchlist?> GetByIdForUserAsync(
        Guid userId,
        Guid watchlistId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Watchlist>> GetUserWatchlistsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByNormalizedNameAsync(
        Guid userId,
        string normalizedName,
        Guid? excludeWatchlistId = null,
        CancellationToken cancellationToken = default);

    Task<Watchlist> AddAsync(Watchlist watchlist, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid watchlistId, CancellationToken cancellationToken = default);

    Task TouchAsync(Guid watchlistId, DateTime utcNow, CancellationToken cancellationToken = default);
}
