using System.Globalization;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public static class AdvancedDiscoverCacheKeys
{
    public static string Create(AdvancedDiscoverCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(criteria), contentLocale);

    public static string Create(AdvancedDiscoverCriteria criteria)
    {
        var genreSegment = criteria.GenreIds.Count == 0
            ? "none"
            : string.Join('-', criteria.GenreIds.OrderBy(id => id));

        var providerSegment = criteria.WatchProviderIds.Count == 0
            ? "wp"
            : string.Join('-', criteria.WatchProviderIds.OrderBy(id => id));

        var monetizationSegment = criteria.WatchMonetizationTypes.Count == 0
            ? "wm"
            : string.Join('-', criteria.WatchMonetizationTypes.OrderBy(type => type));

        return string.Join(
            ':',
            "advanced-discover",
            criteria.MediaType.ToString().ToLowerInvariant(),
            genreSegment,
            criteria.GenreMatch.ToString(),
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
            criteria.Certification?.Trim() ?? "cert",
            criteria.CertificationCountry?.Trim().ToUpperInvariant() ?? "certc",
            criteria.ReleaseTypes.Count == 0
                ? "rt"
                : string.Join('-', criteria.ReleaseTypes.OrderBy(type => type)),
            criteria.WatchRegion?.Trim().ToUpperInvariant() ?? "wr",
            providerSegment,
            monetizationSegment,
            criteria.Sort.ToString(),
            criteria.Page.ToString(CultureInfo.InvariantCulture),
            criteria.PageSize.ToString(CultureInfo.InvariantCulture));
    }
}
