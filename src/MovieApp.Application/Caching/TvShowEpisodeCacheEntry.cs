using MovieApp.Application.Models.TvShows;

namespace MovieApp.Application.Caching;

public sealed class TvShowEpisodeCacheEntry
{
    public required EpisodeResult Result { get; init; }
}
