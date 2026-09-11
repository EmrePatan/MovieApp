using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Caching;

public sealed class TvShowSeasonCacheEntry
{
    public required SeasonResult Result { get; init; }
}
