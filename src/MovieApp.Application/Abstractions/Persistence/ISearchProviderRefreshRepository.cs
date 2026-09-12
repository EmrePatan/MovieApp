using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ISearchProviderRefreshRepository
{
    Task<DateTime?> GetLastRefreshedAtUtcAsync(
        string normalizedQuery,
        SearchContentType contentType,
        int page,
        CancellationToken cancellationToken = default);

    Task SetLastRefreshedAtUtcAsync(
        string normalizedQuery,
        SearchContentType contentType,
        int page,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default);
}
