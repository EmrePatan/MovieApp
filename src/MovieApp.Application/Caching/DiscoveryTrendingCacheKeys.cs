using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class DiscoveryTrendingCacheKeys
{
    public const string Prefix = "discovery-trending:";

    public static string Create(DiscoveryCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(criteria), contentLocale);

    public static string Create(DiscoveryCriteria criteria) =>
        $"{Prefix}{criteria.Type}:{criteria.Page}:{criteria.PageSize}";

    public static string CreateWeekList(DiscoveryCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(
            $"{Prefix}week:{criteria.Type}:{criteria.Page}:{criteria.PageSize}:v2",
            contentLocale);
}
