using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class HomeCacheKeys
{
    public const string Prefix = "home:";

    public const string Version = "v8";

    public static string Create(
        Guid userId,
        SearchContentType type,
        int sectionSize,
        string? contentLocale = null,
        string releaseRegion = "TR",
        long recommendationGeneration = 0)
    {
        var key =
            $"{Prefix}{userId}:{type}:{sectionSize}:{releaseRegion}:g{recommendationGeneration}:{Version}";
        return contentLocale is null
            ? key
            : ContentLocaleCacheKeySegment.Append(key, contentLocale);
    }
}
