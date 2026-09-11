using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class DiscoveryPopularCacheKeys
{
    public const string Prefix = "discovery-popular:";

    public static string Create(DiscoveryCriteria criteria) =>
        $"{Prefix}{criteria.Type}:{criteria.Page}:{criteria.PageSize}";
}
