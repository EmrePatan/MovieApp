using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Search;

namespace MovieApp.Infrastructure.Providers;

public sealed class FakeDiscoveryWatchProviderCatalog : IDiscoveryWatchProviderCatalog
{
    public Task<IReadOnlyList<DiscoveryWatchProviderItem>> GetWatchProvidersAsync(
        SearchContentType mediaType,
        string watchRegion,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DiscoveryWatchProviderItem> providers = watchRegion.Equals("TR", StringComparison.OrdinalIgnoreCase)
            ?
            [
                new DiscoveryWatchProviderItem(8, "Netflix", "/t/p/original/netflix.png", 1),
                new DiscoveryWatchProviderItem(337, "Disney Plus", "/t/p/original/disney.png", 2),
                new DiscoveryWatchProviderItem(119, "Prime Video", "/t/p/original/prime.png", 3),
                new DiscoveryWatchProviderItem(2, "Apple TV Store", "/t/p/original/apple.png", 4),
                new DiscoveryWatchProviderItem(3, "Google Play Movies", "/t/p/original/google.png", 5),
                new DiscoveryWatchProviderItem(10, "Amazon Video", "/t/p/original/amazon.png", 6),
            ]
            : [];

        return Task.FromResult(providers);
    }
}
