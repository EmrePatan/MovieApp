using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Models.WatchProviders;

/// <summary>
/// TMDB provider ids that sell or rent titles rather than offering a subscription stream.
/// Apple TV Store (2), Google Play Movies (3), and Amazon Video (10) are the defaults.
/// Prime Video and Apple TV Plus use different ids and stay in the streaming catalog.
/// </summary>
public static class TransactionalWatchProviderCatalog
{
    public static readonly IReadOnlyList<int> DefaultProviderIds = [2, 3, 10];

    public static IReadOnlySet<int> Resolve(IEnumerable<int>? configuredProviderIds)
    {
        IEnumerable<int> source = configuredProviderIds ?? DefaultProviderIds;
        return source.Where(providerId => providerId > 0).ToHashSet();
    }

    public static bool IncludeInDiscoveryList(IReadOnlyList<WatchMonetizationType> monetizationTypes) =>
        monetizationTypes.Count > 0 &&
        monetizationTypes.All(type => type is WatchMonetizationType.Buy or WatchMonetizationType.Rent);

    public static bool RequiresSameOfferStreamCheck(
        IReadOnlyList<int> watchProviderIds,
        IReadOnlyList<WatchMonetizationType> monetizationTypes,
        IReadOnlySet<int> transactionalProviderIds) =>
        monetizationTypes.Contains(WatchMonetizationType.Stream) &&
        watchProviderIds.Any(transactionalProviderIds.Contains);
}
