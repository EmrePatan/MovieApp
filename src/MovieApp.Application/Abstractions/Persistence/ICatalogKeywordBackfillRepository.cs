using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ICatalogKeywordBackfillRepository
{
    Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectMovieCandidatesAsync(
        int limit,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectTvShowCandidatesAsync(
        int limit,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default);

    Task<CatalogKeywordCoverageSnapshot> GetCoverageAsync(CancellationToken cancellationToken = default);

    Task<bool> IsMovieKeywordSyncedAsync(Guid movieId, CancellationToken cancellationToken = default);

    Task<bool> IsTvShowKeywordSyncedAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
