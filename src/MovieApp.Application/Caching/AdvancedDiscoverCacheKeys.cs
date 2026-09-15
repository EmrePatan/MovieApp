using System.Globalization;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class AdvancedDiscoverCacheKeys
{
    public static string Create(AdvancedDiscoverCriteria criteria)
    {
        var genreSegment = criteria.GenreIds.Count == 0
            ? "none"
            : string.Join('-', criteria.GenreIds.OrderBy(id => id));

        return string.Join(
            ':',
            "advanced-discover",
            criteria.MediaType.ToString().ToLowerInvariant(),
            genreSegment,
            criteria.Year?.ToString(CultureInfo.InvariantCulture) ?? "y",
            criteria.YearFrom?.ToString(CultureInfo.InvariantCulture) ?? "yf",
            criteria.YearTo?.ToString(CultureInfo.InvariantCulture) ?? "yt",
            criteria.MinRating?.ToString(CultureInfo.InvariantCulture) ?? "rmin",
            criteria.MaxRating?.ToString(CultureInfo.InvariantCulture) ?? "rmax",
            criteria.MinVoteCount?.ToString(CultureInfo.InvariantCulture) ?? "vc",
            criteria.MinRuntimeMinutes?.ToString(CultureInfo.InvariantCulture) ?? "rtmin",
            criteria.MaxRuntimeMinutes?.ToString(CultureInfo.InvariantCulture) ?? "rtmax",
            criteria.OriginalLanguage?.Trim().ToLowerInvariant() ?? "lang",
            criteria.OriginCountry?.Trim().ToUpperInvariant() ?? "country",
            criteria.Sort.ToString(),
            criteria.Page.ToString(CultureInfo.InvariantCulture),
            criteria.PageSize.ToString(CultureInfo.InvariantCulture));
    }
}
