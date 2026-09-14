using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class CatalogFollowRepository(ApplicationDbContext dbContext) : ICatalogFollowRepository
{
    public async Task<CatalogFollow?> GetForUserAndContentAsync(
        Guid userId,
        CatalogContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CatalogFollows
            .AsNoTracking()
            .FirstOrDefaultAsync(
                follow =>
                    follow.UserId == userId &&
                    follow.ContentType == contentType &&
                    follow.ContentId == contentId,
                cancellationToken);
    }

    public async Task<CatalogFollow?> GetForUserAndContentForUpdateAsync(
        Guid userId,
        CatalogContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CatalogFollows
            .FirstOrDefaultAsync(
                follow =>
                    follow.UserId == userId &&
                    follow.ContentType == contentType &&
                    follow.ContentId == contentId,
                cancellationToken);
    }

    public async Task<bool> TryAddAsync(CatalogFollow follow, CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.CatalogFollows.Add(follow);
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

    public async Task<bool> RemoveAsync(
        Guid userId,
        CatalogContentType contentType,
        Guid contentId,
        CancellationToken cancellationToken = default)
    {
        var follow = await dbContext.CatalogFollows
            .FirstOrDefaultAsync(
                item =>
                    item.UserId == userId &&
                    item.ContentType == contentType &&
                    item.ContentId == contentId,
                cancellationToken);

        if (follow is null)
        {
            return false;
        }

        dbContext.CatalogFollows.Remove(follow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(IReadOnlyList<CatalogFollow> Follows, int TotalCount)> GetUserFollowsAsync(
        Guid userId,
        int page,
        int pageSize,
        CatalogContentType? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.UserId == userId);

        if (contentType is not null)
        {
            query = query.Where(follow => follow.ContentType == contentType);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var follows = await query
            .OrderByDescending(follow => follow.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (follows, totalCount);
    }

    public async Task<IReadOnlyList<CatalogFollow>> GetEstablishedTvFollowsByTvShowIdsAsync(
        IReadOnlyCollection<Guid> tvShowIds,
        CancellationToken cancellationToken = default)
    {
        if (tvShowIds.Count == 0)
        {
            return [];
        }

        return await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow =>
                follow.ContentType == CatalogContentType.Tv &&
                tvShowIds.Contains(follow.ContentId) &&
                follow.BaselineEstablishedAtUtc != null &&
                follow.NotifyFromUtc != null)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogFollow>> GetMovieFollowsForReleaseCheckAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.ContentType == CatalogContentType.Movie)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetFollowedMovieIdsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.ContentType == CatalogContentType.Movie)
            .Select(follow => follow.ContentId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveMovieFollowsByMovieIdAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var follows = await dbContext.CatalogFollows
            .Where(follow =>
                follow.ContentType == CatalogContentType.Movie &&
                follow.ContentId == movieId)
            .ToListAsync(cancellationToken);

        if (follows.Count == 0)
        {
            return;
        }

        dbContext.CatalogFollows.RemoveRange(follows);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
