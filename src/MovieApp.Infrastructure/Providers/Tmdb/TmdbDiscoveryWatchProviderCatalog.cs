using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb;

public sealed class TmdbDiscoveryWatchProviderCatalog(TmdbApiClient apiClient) : IDiscoveryWatchProviderCatalog
{
    public async Task<IReadOnlyList<DiscoveryWatchProviderItem>> GetWatchProvidersAsync(
        SearchContentType mediaType,
        string watchRegion,
        CancellationToken cancellationToken = default)
    {
        var resource = mediaType == SearchContentType.Tv
            ? "watch/providers/tv"
            : "watch/providers/movie";

        var query = $"watch_region={Uri.EscapeDataString(watchRegion.Trim().ToUpperInvariant())}";
        var response = await apiClient.GetAsync<TmdbWatchProviderCatalogResponseJson>(
            $"{resource}?{query}",
            cancellationToken);

        return TmdbDiscoveryWatchProviderCatalogMapper.ToDiscoveryWatchProviders(response);
    }
}
