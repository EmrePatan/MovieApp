using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Caching;

public sealed class TvShowDetailsCacheEntry
{
    public required TvShowDetailsResult Result { get; init; }
}
