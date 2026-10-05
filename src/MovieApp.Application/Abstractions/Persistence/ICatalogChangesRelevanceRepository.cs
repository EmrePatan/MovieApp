using MovieApp.Application.Models.Changes;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ICatalogChangesRelevanceRepository
{
    Task<IReadOnlyDictionary<int, Guid>> GetRelevantMovieIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, Guid>> GetRelevantTvShowIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, Guid>> GetDiscoveryRelevantMovieIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, Guid>> GetDiscoveryRelevantTvShowIdsByTmdbIdAsync(
        CancellationToken cancellationToken = default);

    Task<CatalogChangesRefreshMaps> GetMovieChangesRefreshMapsForTmdbIdsAsync(
        IReadOnlyCollection<int> changedTmdbIds,
        CancellationToken cancellationToken = default);

    Task<CatalogChangesRefreshMaps> GetTvShowChangesRefreshMapsForTmdbIdsAsync(
        IReadOnlyCollection<int> changedTmdbIds,
        CancellationToken cancellationToken = default);
}