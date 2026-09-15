using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Services.Keywords;

public interface ICatalogKeywordBackfillService
{
    Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectCandidatesAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<CatalogKeywordBackfillBatchResult> ProcessBatchAsync(
        IReadOnlyList<CatalogKeywordBackfillCandidate> candidates,
        CancellationToken cancellationToken = default);

    Task<CatalogKeywordCoverageSnapshot> GetCoverageAsync(CancellationToken cancellationToken = default);
}
