using MovieApp.Application.Models.WatchProviders;
using MovieApp.Contracts.WatchProviders;

namespace MovieApp.Api.Mapping;

public static class WatchProvidersContractMapper
{
    public static WatchProvidersResponse ToResponse(WatchProvidersResult result) =>
        new(
            result.Region,
            result.Providers.Select(ToProviderResponse).ToList(),
            result.AttributionLink);

    private static WatchProviderResponse ToProviderResponse(WatchProviderResult provider) =>
        new(
            provider.ProviderId,
            provider.Name,
            provider.LogoPath,
            provider.DisplayPriority,
            provider.AvailabilityTypes.Select(MapAvailabilityType).ToList(),
            provider.Link);

    private static string MapAvailabilityType(WatchProviderAvailabilityType availabilityType) =>
        availabilityType switch
        {
            WatchProviderAvailabilityType.Flatrate => "flatrate",
            WatchProviderAvailabilityType.Rent => "rent",
            WatchProviderAvailabilityType.Buy => "buy",
            _ => availabilityType.ToString().ToLowerInvariant()
        };
}
