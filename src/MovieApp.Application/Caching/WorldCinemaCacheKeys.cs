using System.Globalization;
using MovieApp.Application.Models.Discovery;

namespace MovieApp.Application.Caching;

public static class WorldCinemaCacheKeys
{
    public static string Create(WorldCinemaCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(
            string.Join(
                ':',
                "discovery-world-cinema",
                criteria.MediaType.ToString().ToLowerInvariant(),
                criteria.OriginCountry.Trim().ToUpperInvariant(),
                criteria.Sort.ToString(),
                criteria.Page,
                criteria.PageSize,
                criteria.GenreIds.Count == 0
                    ? "g"
                    : string.Join('-', criteria.GenreIds.OrderBy(id => id)),
                criteria.GenreMatch.ToString(),
                criteria.YearFrom?.ToString(CultureInfo.InvariantCulture) ?? "yf",
                criteria.YearTo?.ToString(CultureInfo.InvariantCulture) ?? "yt",
                criteria.MinRating?.ToString(CultureInfo.InvariantCulture) ?? "rmin",
                criteria.MaxRating?.ToString(CultureInfo.InvariantCulture) ?? "rmax",
                criteria.MinVoteCount?.ToString(CultureInfo.InvariantCulture) ?? "vc",
                criteria.MinRuntimeMinutes?.ToString(CultureInfo.InvariantCulture) ?? "rtmin",
                criteria.MaxRuntimeMinutes?.ToString(CultureInfo.InvariantCulture) ?? "rtmax",
                criteria.OriginalLanguage?.Trim().ToLowerInvariant() ?? "lang",
                criteria.KeywordIds.Count == 0
                    ? "kw"
                    : string.Join('-', criteria.KeywordIds.OrderBy(id => id)),
                criteria.TvStatuses.Count == 0
                    ? "tvs"
                    : string.Join('-', criteria.TvStatuses.OrderBy(status => status)),
                "v5"),
            contentLocale);
}
