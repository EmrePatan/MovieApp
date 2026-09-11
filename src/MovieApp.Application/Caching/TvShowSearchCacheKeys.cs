using MovieApp.Application.Common;

namespace MovieApp.Application.Caching;

public static class TvShowSearchCacheKeys
{
    public const string Prefix = "tvshow-search:";

    public static string Create(string query, int page, int pageSize) =>
        $"{Prefix}{QueryNormalizer.Normalize(query)}:page:{page}:size:{pageSize}";
}
