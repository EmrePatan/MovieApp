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

    Task<IReadOnlyList<CatalogUpcomingItemResult>> GetFollowedMovieReleasesForHomeAsync(
        Guid userId,
        DateOnly today,
        string region,
        int limit,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<IReadOnlyList<CatalogUpcomingItemResult>> GetFollowedTvPremieresForHomeAsync(
        Guid userId,
        DateOnly today,
        int limit,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    Task<IReadOnlyList<CatalogUpcomingItemResult>> GetFollowedEpisodeReleasesForHomeAsync(
        Guid userId,
        DateOnly today,
        int limit,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
