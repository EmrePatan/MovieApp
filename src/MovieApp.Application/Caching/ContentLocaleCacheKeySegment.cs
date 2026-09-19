using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Caching;

public static class ContentLocaleCacheKeySegment
{
    public static string Append(string cacheKey, string contentLocale)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return cacheKey;
        }

        return $"{cacheKey}:loc:{Normalize(contentLocale)}";
    }

    public static string Normalize(string contentLocale) =>
        contentLocale.Trim().ToLowerInvariant();
}
