using System.Globalization;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class DiscoveryWatchProvidersCacheKeys
{
    public static string Create(SearchContentType mediaType, string watchRegion) =>
        string.Join(
            ':',
            "discovery-watch-providers",
            mediaType.ToString().ToLowerInvariant(),
            watchRegion.Trim().ToUpperInvariant(),
            "v1");
}

public sealed class DiscoveryWatchProvidersCacheEntry
{
    public required IReadOnlyList<Models.Discovery.DiscoveryWatchProviderItem> Providers { get; init; }
}
