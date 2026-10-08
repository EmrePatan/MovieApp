using MovieApp.Application.Services.Keywords;

namespace MovieApp.Application.Caching;

public static class KeywordDiscoverCacheKeys
{
    public const string Prefix = "discovery-keywords:v1:";

    public static string Create(string query, string contentLocale, int page, int pageSize)
    {
        var locale = KeywordDiscoverLocalizationSupport.NormalizeLocale(contentLocale);
        var normalizedQuery = query.Trim();
        return $"{Prefix}{locale}:{page}:{pageSize}:{normalizedQuery}";
    }
}
