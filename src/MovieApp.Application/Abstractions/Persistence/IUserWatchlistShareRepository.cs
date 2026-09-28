using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IUserWatchlistShareRepository
{
    Task<UserWatchlistShare?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserWatchlistShare?> GetActiveByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<UserWatchlistShare> AddAsync(UserWatchlistShare share, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserWatchlistShare share, CancellationToken cancellationToken = default);
}
