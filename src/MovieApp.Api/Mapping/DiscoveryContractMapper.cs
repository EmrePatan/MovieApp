using MovieApp.Application.Models.Discovery;
using MovieApp.Contracts.Discovery;

namespace MovieApp.Api.Mapping;

public static class DiscoveryContractMapper
{
    public static DiscoveryWatchProvidersResponse ToWatchProvidersResponse(
        string watchRegion,
        string mediaType,
        IReadOnlyList<DiscoveryWatchProviderItem> providers) =>
        new(
            watchRegion,
            mediaType,
            providers.Select(ToWatchProviderResponse).ToList());

    private static DiscoveryWatchProviderResponse ToWatchProviderResponse(DiscoveryWatchProviderItem provider) =>
        new(
            provider.ProviderId,
            provider.Name,
            provider.LogoPath,
            provider.DisplayPriority);
}
