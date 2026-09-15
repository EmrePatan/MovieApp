using MovieApp.Application.Models.Videos;

namespace MovieApp.Application.Caching;

public sealed class VideosCacheEntry
{
    public VideosResult Result { get; init; } = new(null);
}
