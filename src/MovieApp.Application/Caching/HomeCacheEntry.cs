using MovieApp.Application.Models.Home;

namespace MovieApp.Application.Caching;

public sealed class HomeCacheEntry
{
    public HomeResult Result { get; init; } = new([], false);
}
