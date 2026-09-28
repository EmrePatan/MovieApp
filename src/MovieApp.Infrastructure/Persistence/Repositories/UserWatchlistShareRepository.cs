using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class UserWatchlistShareRepository(ApplicationDbContext dbContext) : IUserWatchlistShareRepository
{
    public async Task<UserWatchlistShare?> GetByWatchlistIdAsync(
        Guid watchlistId,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserWatchlistShares
            .FirstOrDefaultAsync(share => share.WatchlistId == watchlistId, cancellationToken);

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
            .Include(share => share.Watchlist)
            .FirstOrDefaultAsync(
                share => share.IsActive && share.TokenHash == tokenHash,
                cancellationToken);

    public async Task<IReadOnlyList<UserWatchlistShare>> ListActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.UserWatchlistShares
            .AsNoTracking()
            .Include(share => share.Watchlist)
            .Where(share => share.UserId == userId && share.IsActive)
            .OrderBy(share => share.Watchlist!.Name)
            .ToListAsync(cancellationToken);

    public async Task<UserWatchlistShare> AddAsync(
        UserWatchlistShare share,
        CancellationToken cancellationToken = default)
    {
        dbContext.UserWatchlistShares.Add(share);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            DbUpdateExceptionExtensions.IsUserWatchlistShareUniqueViolation(exception) ||
            DbUpdateExceptionExtensions.IsUniqueConstraintViolation(
                exception,
                "IX_user_watchlist_shares_UserId"))
        {
            throw new ConflictException(
                "Watchlist share could not be created because of a legacy unique index on UserId.");
        }

        return share;
    }

    public async Task UpdateAsync(UserWatchlistShare share, CancellationToken cancellationToken = default)
    {
        dbContext.UserWatchlistShares.Update(share);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
