using MovieApp.Application.Models.Keywords;

namespace MovieApp.Application.Services.Keywords;

public interface ICatalogKeywordBackfillItemProcessor
{
    Task<CatalogKeywordBackfillItemOutcome> ProcessAsync(
        CatalogKeywordBackfillCandidate candidate,
        CancellationToken cancellationToken = default);
}
