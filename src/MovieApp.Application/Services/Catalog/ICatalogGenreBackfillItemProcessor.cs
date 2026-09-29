using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Services.Catalog;

public interface ICatalogGenreBackfillItemProcessor
{
    Task<CatalogGenreBackfillItemOutcome> ProcessAsync(
        CatalogGenreBackfillCandidate candidate,
        CancellationToken cancellationToken = default);
}
