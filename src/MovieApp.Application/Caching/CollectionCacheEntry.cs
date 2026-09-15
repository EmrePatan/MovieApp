using MovieApp.Application.Models.Collections;

namespace MovieApp.Application.Caching;

public sealed class CollectionCacheEntry
{
    public CollectionDetailResult Result { get; init; } = new(
        0,
        string.Empty,
        null,
        null,
        null,
        []);
}
