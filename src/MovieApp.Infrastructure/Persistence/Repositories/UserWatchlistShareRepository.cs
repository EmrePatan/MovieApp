using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class UserWatchlistShareRepository(ApplicationDbContext dbContext) : IUserWatchlistShareRepository
{
    public async Task<UserWatchlistShare?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserWatchlistShares
            .FirstOrDefaultAsync(share => share.UserId == userId, cancellationToken);

    public async Task<UserWatchlistShare?> GetActiveByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserWatchlistShares
            .AsNoTracking()
            .Include(share => share.User)
            .FirstOrDefaultAsync(
                share => share.IsActive && share.TokenHash == tokenHash,
                cancellationToken);

    public async Task<UserWatchlistShare> AddAsync(
        UserWatchlistShare share,
        CancellationToken cancellationToken = default)
    {
        dbContext.UserWatchlistShares.Add(share);
        await dbContext.SaveChangesAsync(cancellationToken);
        return share;
    }

    public async Task UpdateAsync(UserWatchlistShare share, CancellationToken cancellationToken = default)
    {
        dbContext.UserWatchlistShares.Update(share);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
