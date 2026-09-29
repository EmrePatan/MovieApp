namespace MovieApp.Application.Models.Discovery;

public static class DiscoveryWatchProviderListFilter
{
    public static IReadOnlyList<DiscoveryWatchProviderItem> Apply(
        IReadOnlyList<DiscoveryWatchProviderItem> providers,
        IReadOnlySet<int> transactionalProviderIds,
        bool includeTransactionalProviders)
    {
        if (includeTransactionalProviders || transactionalProviderIds.Count == 0)
        {
            return providers;
        }

        return providers
            .Where(provider => !transactionalProviderIds.Contains(provider.ProviderId))
            .ToList();
    }
}
