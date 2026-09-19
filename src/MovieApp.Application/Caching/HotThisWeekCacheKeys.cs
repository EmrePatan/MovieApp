using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class HotThisWeekCacheKeys
{
    public const string Prefix = "hot-this-week:";

    public const string Version = "v3";

    public static string Create(SearchContentType type, int maxItems, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(type, maxItems), contentLocale);

    public static string Create(SearchContentType type, int maxItems) =>
        $"{Prefix}{type}:{maxItems}:{Version}";
}
