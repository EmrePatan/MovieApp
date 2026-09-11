using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class SearchHistoryRepository(ApplicationDbContext dbContext) : ISearchHistoryRepository
{
    public async Task RecordSearchAsync(
        Guid userId,
        string query,
        string normalizedQuery,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var latest = await dbContext.SearchHistories
            .Where(history => history.UserId == userId)
            .OrderByDescending(history => history.SearchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && latest.NormalizedQuery == normalizedQuery)
        {
            latest.UpdateSearchedAt(utcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var history = SearchHistory.Create(userId, query, normalizedQuery, utcNow);
        dbContext.SearchHistories.Add(history);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<SearchHistory> Items, int TotalCount)> GetUserHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SearchHistories
            .AsNoTracking()
            .Where(history => history.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(history => history.SearchedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> DeleteAsync(
        Guid userId,
        Guid historyId,
        CancellationToken cancellationToken = default)
    {
        var history = await dbContext.SearchHistories
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.Id == historyId,
                cancellationToken);

        if (history is null)
        {
            return false;
        }

        dbContext.SearchHistories.Remove(history);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await dbContext.SearchHistories
            .Where(history => history.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
