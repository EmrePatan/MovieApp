using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ISearchHistoryRepository
{
    Task RecordSearchAsync(
        Guid userId,
        string query,
        string normalizedQuery,
        DateTime utcNow,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SearchHistory> Items, int TotalCount)> GetUserHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid historyId, CancellationToken cancellationToken = default);

    Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default);
}
