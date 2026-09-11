using System.Globalization;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class UnifiedSearchCacheKeys
{
    public const string Prefix = "search:";

    public static string Create(SearchCriteria criteria)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(criteria.Query)
            ? "_"
            : Common.QueryNormalizer.Normalize(criteria.Query);

        return string.Join(
            ':',
            Prefix,
            normalizedQuery,
            criteria.Type,
            criteria.GenreId?.ToString() ?? "_",
            criteria.Year?.ToString(CultureInfo.InvariantCulture) ?? "_",
            criteria.MinRating?.ToString("0.##", CultureInfo.InvariantCulture) ?? "_",
            criteria.MaxRating?.ToString("0.##", CultureInfo.InvariantCulture) ?? "_",
            criteria.Sort,
            criteria.Page.ToString(CultureInfo.InvariantCulture),
            criteria.PageSize.ToString(CultureInfo.InvariantCulture));
    }
}
