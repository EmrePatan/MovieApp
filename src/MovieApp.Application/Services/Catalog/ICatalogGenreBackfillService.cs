using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Services.Catalog;

public interface ICatalogGenreBackfillService
{
    bool IsEnabled { get; }

    Task<IReadOnlyList<CatalogGenreBackfillCandidate>> SelectCandidatesAsync(
        int batchSize,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken cancellationToken = default);

    Task<CatalogGenreBackfillBatchResult> ProcessBatchAsync(
        IReadOnlyList<CatalogGenreBackfillCandidate> candidates,
        ISet<Guid> runExcludeIds,
        CancellationToken cancellationToken = default);

    Task<CatalogGenreBackfillCoverageSnapshot> GetCoverageAsync(CancellationToken cancellationToken = default);

    Task<CatalogGenreBackfillRunResult> RunAsync(CancellationToken cancellationToken = default);
}
