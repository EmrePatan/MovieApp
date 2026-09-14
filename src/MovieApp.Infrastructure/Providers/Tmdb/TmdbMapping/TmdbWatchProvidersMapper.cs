using MovieApp.Application.Models.WatchProviders;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;

internal static class TmdbWatchProvidersMapper
{
    public static WatchProvidersResult ToWatchProvidersResult(
        TmdbWatchProvidersResponseJson response,
        string region)
    {
        if (!response.Results.TryGetValue(region, out var regionData))
        {
            return new WatchProvidersResult(region, [], null);
        }

        var merged = new Dictionary<int, ProviderAccumulator>();

        AddProviders(merged, regionData.Flatrate, WatchProviderAvailabilityType.Flatrate);
        AddProviders(merged, regionData.Rent, WatchProviderAvailabilityType.Rent);
        AddProviders(merged, regionData.Buy, WatchProviderAvailabilityType.Buy);

        var providers = merged.Values
            .Select(accumulator => new WatchProviderResult(
                accumulator.ProviderId,
                accumulator.Name,
                accumulator.LogoPath,
                accumulator.DisplayPriority,
                accumulator.AvailabilityTypes
                    .Distinct()
                    .OrderBy(type => type)
                    .ToList(),
                regionData.Link))
            .OrderBy(provider => provider.DisplayPriority)
            .ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new WatchProvidersResult(region, providers, regionData.Link);
    }

    private static void AddProviders(
        IDictionary<int, ProviderAccumulator> merged,
        IReadOnlyList<TmdbWatchProviderJson>? providers,
        WatchProviderAvailabilityType availabilityType)
    {
        if (providers is null)
        {
            return;
        }

        foreach (var provider in providers)
        {
            if (!merged.TryGetValue(provider.ProviderId, out var accumulator))
            {
                accumulator = new ProviderAccumulator(
                    provider.ProviderId,
                    provider.ProviderName,
                    provider.LogoPath,
                    provider.DisplayPriority);
                merged[provider.ProviderId] = accumulator;
            }

            accumulator.AvailabilityTypes.Add(availabilityType);
            accumulator.DisplayPriority = Math.Min(accumulator.DisplayPriority, provider.DisplayPriority);
        }
    }

    private sealed class ProviderAccumulator(
        int providerId,
        string name,
        string? logoPath,
        int displayPriority)
    {
        public int ProviderId { get; } = providerId;

        public string Name { get; } = name;

        public string? LogoPath { get; } = logoPath;

        public int DisplayPriority { get; set; } = displayPriority;

        public List<WatchProviderAvailabilityType> AvailabilityTypes { get; } = [];
    }
}
