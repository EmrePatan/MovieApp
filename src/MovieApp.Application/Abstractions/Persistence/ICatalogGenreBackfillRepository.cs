using MovieApp.Application.Models.Catalog;
using MovieApp.Domain.Enums;

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

    Task UpsertRepairAttemptAsync(
        CatalogContentType contentType,
        Guid catalogId,
        CatalogGenreRepairAttemptOutcome outcome,
        DateTime attemptedAtUtc,
        DateTime nextEligibleAtUtc,
        CancellationToken cancellationToken = default);

    Task ClearRepairAttemptAsync(
        CatalogContentType contentType,
        Guid catalogId,
        CancellationToken cancellationToken = default);
}
