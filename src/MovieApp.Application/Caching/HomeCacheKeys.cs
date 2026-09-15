using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class HomeCacheKeys
{
    public const string Prefix = "home:";

    public const string Version = "v3";

    public static string Create(Guid userId, SearchContentType type, int sectionSize) =>
        $"{Prefix}{userId}:{type}:{sectionSize}:{Version}";
}
