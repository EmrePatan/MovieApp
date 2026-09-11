using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Caching;

public sealed class TvShowSearchCacheEntry
{
    public required PaginatedResult<TvShowSearchResult> Result { get; init; }
}
