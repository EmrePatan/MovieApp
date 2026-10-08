using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Caching;

public sealed class LibrarySearchCacheEntry
{
    public required PaginatedResult<LibraryItemResult> Result { get; init; }
}
