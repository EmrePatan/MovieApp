namespace MovieApp.Application.Caching;

public static class HotThisWeekTrendingSnapshotCacheKeys
{
    public const string Prefix = "hot-this-week-trending-snapshot:";

    public const string Version = "v1";

    public static string Canonical => $"{Prefix}{Version}";
}
