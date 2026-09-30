using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Services.Keywords;

public interface IMdbListKeywordBackfillService
{
    Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectCandidatesAsync(
        int batchSize,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default);

    Task<CatalogKeywordBackfillBatchResult> ProcessBatchAsync(
        IReadOnlyList<CatalogKeywordBackfillCandidate> candidates,
        CancellationToken cancellationToken = default);
}
