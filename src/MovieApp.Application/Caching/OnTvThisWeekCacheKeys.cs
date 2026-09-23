using MovieApp.Application.Models.Discovery;

namespace MovieApp.Application.Caching;

public static class OnTvThisWeekCacheKeys
{
    public static string Create(OnTvThisWeekCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(
            string.Join(
                ':',
                "discovery-on-tv-this-week",
                criteria.Page,
                criteria.PageSize,
                "v2"),
            contentLocale);
}
