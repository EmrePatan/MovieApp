using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class TvShowFollowRepository(ApplicationDbContext dbContext) : ITvShowFollowRepository
{
    public async Task<TvShowFollow?> GetForUserAndTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.TvShowFollows
            .AsNoTracking()
            .FirstOrDefaultAsync(
                follow => follow.UserId == userId && follow.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<TvShowFollow?> GetForUserAndTvShowForUpdateAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.TvShowFollows
            .FirstOrDefaultAsync(
                follow => follow.UserId == userId && follow.TvShowId == tvShowId,
                cancellationToken);
    }

    public async Task<bool> TryAddAsync(TvShowFollow follow, CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.TvShowFollows.Add(follow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionExtensions.IsUniqueConstraintViolation(exception))
        {
            dbContext.Entry(follow).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<bool> RemoveForTvShowAsync(
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken = default)
    {
        var follow = await dbContext.TvShowFollows
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.TvShowId == tvShowId,
                cancellationToken);

        if (follow is null)
        {
            return false;
        }

        dbContext.TvShowFollows.Remove(follow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(IReadOnlyList<TvShowFollow> Follows, int TotalCount)> GetUserFollowsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.TvShowFollows
            .AsNoTracking()
            .Where(follow => follow.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var follows = await query
            .Include(follow => follow.TvShow)
            .OrderByDescending(follow => follow.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (follows, totalCount);
    }
}
