using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class WatchlistRepository(ApplicationDbContext dbContext) : IWatchlistRepository
{
    public async Task<Watchlist?> GetByIdForUserAsync(
        Guid userId,
        Guid watchlistId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Watchlists
            .AsNoTracking()
            .FirstOrDefaultAsync(
                watchlist => watchlist.Id == watchlistId && watchlist.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Watchlist>> GetUserWatchlistsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Watchlists
            .AsNoTracking()
            .Where(watchlist => watchlist.UserId == userId)
            .OrderByDescending(watchlist => watchlist.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNormalizedNameAsync(
        Guid userId,
        string normalizedName,
        Guid? excludeWatchlistId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Watchlists
            .AsNoTracking()
            .Where(watchlist => watchlist.UserId == userId && watchlist.NormalizedName == normalizedName);

        if (excludeWatchlistId is not null)
        {
            query = query.Where(watchlist => watchlist.Id != excludeWatchlistId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<Watchlist> AddAsync(Watchlist watchlist, CancellationToken cancellationToken = default)
    {
        dbContext.Watchlists.Add(watchlist);
        await dbContext.SaveChangesAsync(cancellationToken);
        return watchlist;
    }

    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid watchlistId,
        CancellationToken cancellationToken = default)
    {
        var watchlist = await dbContext.Watchlists
            .FirstOrDefaultAsync(
                item => item.Id == watchlistId && item.UserId == userId,
                cancellationToken);

        if (watchlist is null)
        {
            return false;
        }

        dbContext.Watchlists.Remove(watchlist);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task TouchAsync(
        Guid watchlistId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var watchlist = await dbContext.Watchlists
            .FirstOrDefaultAsync(item => item.Id == watchlistId, cancellationToken);

        if (watchlist is null)
        {
            return;
        }

        watchlist.Touch(utcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
