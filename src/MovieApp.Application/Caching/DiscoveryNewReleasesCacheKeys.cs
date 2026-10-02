using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class DiscoveryNewReleasesCacheKeys
{
    public const string Prefix = "discovery-new-releases:";
    public const string Version = "v2";

    public static string Create(DiscoveryCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(criteria), contentLocale);

    public static string Create(DiscoveryCriteria criteria) =>
        $"{Prefix}{Version}:{criteria.Type}:{criteria.Page}:{criteria.PageSize}";
}
