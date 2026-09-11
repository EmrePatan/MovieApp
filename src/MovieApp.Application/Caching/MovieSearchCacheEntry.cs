using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Caching;

public sealed class MovieSearchCacheEntry
{
    public required PaginatedResult<MovieSearchResult> Result { get; init; }
}
