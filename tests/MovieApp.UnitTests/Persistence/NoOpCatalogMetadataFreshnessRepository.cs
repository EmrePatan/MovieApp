using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Catalog;

namespace MovieApp.UnitTests.Persistence;

internal sealed class NoOpCatalogMetadataFreshnessRepository : ICatalogMetadataFreshnessRepository
{
    public Task MarkProviderDiscoverySeenForMoviesAsync(
        IReadOnlyList<Guid> movieIds,
        DateTime seenAtUtc,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task MarkProviderDiscoverySeenForTvShowsAsync(
        IReadOnlyList<Guid> tvShowIds,
        DateTime seenAtUtc,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<int> CountStaleDiscoveryRelevantMoviesAsync(
        DateTime staleBeforeUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<int> CountStaleDiscoveryRelevantTvShowsAsync(
        DateTime staleBeforeUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task<IReadOnlyList<Guid>> SelectStaleDiscoveryRelevantMovieIdsAsync(
        DateTime staleBeforeUtc,
        int take,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);

    public Task<IReadOnlyList<Guid>> SelectStaleDiscoveryRelevantTvShowIdsAsync(
        DateTime staleBeforeUtc,
        int take,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);

    public Task<CatalogMetadataFreshnessDistribution> GetDiscoveryFreshnessDistributionAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new CatalogMetadataFreshnessDistribution(0, 0, 0, 0, 0, 0));
}
