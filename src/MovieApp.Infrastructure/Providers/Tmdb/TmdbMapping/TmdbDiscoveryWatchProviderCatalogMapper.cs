using MovieApp.Application.Models.Discovery;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbDiscoveryWatchProviderCatalogMapper
{
    public static IReadOnlyList<DiscoveryWatchProviderItem> ToDiscoveryWatchProviders(
        TmdbWatchProviderCatalogResponseJson? response)
    {
        if (response?.Results is null || response.Results.Count == 0)
        {
            return [];
        }

        return response.Results
            .Where(item => item.ProviderId > 0 && !string.IsNullOrWhiteSpace(item.ProviderName))
            .OrderBy(item => item.DisplayPriority)
            .ThenBy(item => item.ProviderName, StringComparer.OrdinalIgnoreCase)
            .Select(item => new DiscoveryWatchProviderItem(
                item.ProviderId,
                item.ProviderName.Trim(),
                item.LogoPath,
                item.DisplayPriority))
            .ToList();
    }
}
