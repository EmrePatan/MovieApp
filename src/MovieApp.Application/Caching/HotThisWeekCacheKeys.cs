using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class HotThisWeekCacheKeys
{
    public const string Prefix = "hot-this-week:";

    public const string Version = "v5";

    public static string Create(SearchContentType type, int maxItems, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(type, maxItems), contentLocale);

    public static string Create(SearchContentType type, int maxItems) =>
        $"{Prefix}{type}:{maxItems}:{Version}";

    public static long ResolveSnapshotGeneration(HotThisWeekTrendingSnapshotEntry? snapshot) =>
        snapshot is null ? 0 : snapshot.RefreshedAt.UtcTicks;

    public static string CreateWeeklyPool(string contentLocale, long snapshotGeneration) =>
        ContentLocaleCacheKeySegment.Append(
            $"{Prefix}weekly-pool:{Version}:snap-{snapshotGeneration}",
            contentLocale);
}
