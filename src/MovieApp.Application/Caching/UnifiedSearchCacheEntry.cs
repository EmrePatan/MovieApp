using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public sealed class UnifiedSearchCacheEntry
{
    public required PaginatedResult<SearchItem> Result { get; init; }
}
