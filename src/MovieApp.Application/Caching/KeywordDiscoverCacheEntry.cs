using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Caching;

public sealed class KeywordDiscoverCacheEntry
{
    public required PaginatedResult<KeywordDiscoverItem> Result { get; init; }
}
