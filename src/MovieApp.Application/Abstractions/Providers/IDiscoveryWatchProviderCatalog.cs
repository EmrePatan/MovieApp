using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Abstractions.Providers;

public interface IDiscoveryWatchProviderCatalog
{
    Task<IReadOnlyList<DiscoveryWatchProviderItem>> GetWatchProvidersAsync(
        SearchContentType mediaType,
        string watchRegion,
        CancellationToken cancellationToken = default);
}
