namespace MovieApp.Application.Models.WatchProviders;

/// <summary>
/// Applies product semantics for title-level Where to Watch API responses.
/// </summary>
public static class TitleWatchProvidersPresentationFilter
{
    public static WatchProvidersResult Apply(WatchProvidersResult result)
    {
        var subscriptionProviders = result.Providers
            .Where(provider => provider.AvailabilityTypes.Contains(WatchProviderAvailabilityType.Flatrate))
            .Select(NormalizeToSubscriptionOnly)
            .ToList();

        return result with { Providers = subscriptionProviders };
    }

    private static WatchProviderResult NormalizeToSubscriptionOnly(WatchProviderResult provider) =>
        provider with
        {
            AvailabilityTypes = [WatchProviderAvailabilityType.Flatrate],
        };
}
