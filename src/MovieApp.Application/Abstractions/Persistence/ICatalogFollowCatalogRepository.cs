using MovieApp.Application.Models.CatalogFollows;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ICatalogFollowCatalogRepository
{
    Task<(IReadOnlyList<CatalogFollowItemResult> Items, int TotalCount)> GetFollowingCatalogAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CatalogUpcomingItemResult> Items, int TotalCount)> GetUpcomingCatalogAsync(
        Guid? userId,
        int page,
        int pageSize,
        DateOnly today,
        string region,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<CatalogUpcomingItemResult> Items, int TotalCount)> GetFollowedUpcomingCatalogAsync(
        Guid userId,
        int page,
        int pageSize,
        DateOnly today,
        string region,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogUpcomingItemResult>> GetFollowedUpcomingForHomeAsync(
        Guid userId,
        DateOnly today,
        string region,
        int limit,
        CancellationToken cancellationToken = default);
}
