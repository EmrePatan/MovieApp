using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class SearchRefreshLockKeys
{
    public const string Prefix = "search-refresh-lock:";

    public static string Create(SearchCriteria criteria)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(criteria.Query)
            ? "_"
            : Common.QueryNormalizer.Normalize(criteria.Query);

        return string.Join(
            ':',
            Prefix.TrimEnd(':'),
            normalizedQuery,
            criteria.Type,
            criteria.Page.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
