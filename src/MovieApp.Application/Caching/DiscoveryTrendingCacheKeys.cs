using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class DiscoveryTrendingCacheKeys
{
    public const string Prefix = "discovery-trending:";

    public static string Create(DiscoveryCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(criteria), contentLocale);

    public static string Create(DiscoveryCriteria criteria) =>
        $"{Prefix}{criteria.Type}:{criteria.Page}:{criteria.PageSize}";
}
