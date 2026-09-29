using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ICatalogGenreBackfillRepository
{
    Task<IReadOnlyList<CatalogGenreBackfillCandidate>> SelectMovieCandidatesAsync(
        int limit,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogGenreBackfillCandidate>> SelectTvShowCandidatesAsync(
        int limit,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default);

    Task<bool> MovieHasGenresAsync(Guid movieId, CancellationToken cancellationToken = default);

    Task<bool> TvShowHasGenresAsync(Guid tvShowId, CancellationToken cancellationToken = default);

    Task<CatalogGenreBackfillCoverageSnapshot> GetCoverageAsync(CancellationToken cancellationToken = default);
}
