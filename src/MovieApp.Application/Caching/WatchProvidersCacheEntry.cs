using MovieApp.Application.Models.WatchProviders;

namespace MovieApp.Application.Caching;

public sealed class WatchProvidersCacheEntry
{
    public WatchProvidersResult Result { get; init; } = new("TR", [], null);
}
