using MovieApp.Application.Models.Catalog;

namespace MovieApp.Application.Caching;

internal sealed class DetailKeywordCacheEntry
{
    public IReadOnlyList<CatalogKeywordSummary> Keywords { get; init; } = [];
}
