using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ICatalogMetadataFreshnessRepository
{
    Task MarkProviderDiscoverySeenForMoviesAsync(
        IReadOnlyList<Guid> movieIds,
        DateTime seenAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkProviderDiscoverySeenForTvShowsAsync(
        IReadOnlyList<Guid> tvShowIds,
        DateTime seenAtUtc,
        CancellationToken cancellationToken = default);

    Task<int> CountStaleDiscoveryRelevantMoviesAsync(
        DateTime staleBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<int> CountStaleDiscoveryRelevantTvShowsAsync(
        DateTime staleBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> SelectStaleDiscoveryRelevantMovieIdsAsync(
        DateTime staleBeforeUtc,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> SelectStaleDiscoveryRelevantTvShowIdsAsync(
        DateTime staleBeforeUtc,
        int take,
        CancellationToken cancellationToken = default);

    Task<CatalogMetadataFreshnessDistribution> GetDiscoveryFreshnessDistributionAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
