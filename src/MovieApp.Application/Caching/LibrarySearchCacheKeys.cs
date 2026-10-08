using MovieApp.Application.Common;
using MovieApp.Application.Models.Library;

namespace MovieApp.Application.Caching;

public static class LibrarySearchCacheKeys
{
    public const string Prefix = "library-search:v1:";

    public static string Create(
        Guid userId,
        long generation,
        string contentLocale,
        LibrarySearchCriteria criteria)
    {
        var query = QueryNormalizer.Normalize(criteria.Query);
        return ContentLocaleCacheKeySegment.Append(
            $"{Prefix}{userId:N}:g{generation}:{criteria.MediaType}:{criteria.Page}:{criteria.PageSize}:{query}",
            contentLocale);
    }
}
