using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IMdbListKeywordBackfillRepository
{
    Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectMovieCandidatesAsync(
        int limit,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectTvShowCandidatesAsync(
        int limit,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default);
}
