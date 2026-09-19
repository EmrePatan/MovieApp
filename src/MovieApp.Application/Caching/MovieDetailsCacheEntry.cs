using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Caching;

public sealed class MovieDetailsCacheEntry
{
    public required MovieDetailsResult Result { get; init; }
}
