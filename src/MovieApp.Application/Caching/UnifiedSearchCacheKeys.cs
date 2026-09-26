using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class UnifiedSearchCacheKeys
{
    public const string Prefix = "search:";

    public static string Create(SearchCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(criteria), contentLocale);

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
            ResolvePageSegment(criteria),
            criteria.PageSize.ToString(CultureInfo.InvariantCulture));
    }

    private static string ResolvePageSegment(SearchCriteria criteria)
    {
        if (!string.IsNullOrWhiteSpace(criteria.Cursor))
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(criteria.Cursor));
            return "cursor:" + Convert.ToHexString(hash.AsSpan(0, 8));
        }

        return criteria.Page.ToString(CultureInfo.InvariantCulture);
    }
}
