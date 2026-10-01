using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Catalog;

namespace MovieApp.UnitTests.Keywords;

internal sealed class NoOpCatalogTitleKeywordReadRepository : ICatalogTitleKeywordReadRepository
{
    public Task<IReadOnlyList<CatalogKeywordSummary>> GetLocalizedKeywordsForMovieAsync(
        Guid movieId,
        string contentLocale,
        int maxCount,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CatalogKeywordSummary>>([]);

    public Task<IReadOnlyList<CatalogKeywordSummary>> GetLocalizedKeywordsForTvShowAsync(
        Guid tvShowId,
        string contentLocale,
        int maxCount,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CatalogKeywordSummary>>([]);
}
