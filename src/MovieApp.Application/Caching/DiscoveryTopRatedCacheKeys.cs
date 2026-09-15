using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class DiscoveryTopRatedCacheKeys
{
    public const string Prefix = "discovery-top-rated:";

    public const string Version = "v2";

    public static string Create(DiscoveryCriteria criteria) =>
        $"{Prefix}{criteria.Type}:{criteria.Page}:{criteria.PageSize}:{Version}";
}
