using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class DiscoveryTrendingCacheKeys
{
    public const string Prefix = "discovery-trending:";

    public const string WeekListVersion = "v3";

    public static string Create(DiscoveryCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(criteria), contentLocale);

    public static string Create(DiscoveryCriteria criteria) =>
        $"{Prefix}{criteria.Type}:{criteria.Page}:{criteria.PageSize}";

    public static string CreateWeekList(
        DiscoveryCriteria criteria,
        string contentLocale,
        HotThisWeekTrendingSnapshotEntry? snapshot) =>
        ContentLocaleCacheKeySegment.Append(
            CreateWeekList(criteria, HotThisWeekCacheKeys.ResolveSnapshotGeneration(snapshot)),
            contentLocale);

    public static string CreateWeekList(DiscoveryCriteria criteria, long snapshotGeneration) =>
        $"{Prefix}week:{criteria.Type}:{criteria.Page}:{criteria.PageSize}:{WeekListVersion}:snap-{snapshotGeneration}";
}
