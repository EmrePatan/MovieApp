using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Caching;

public static class DiscoveryBrowseCacheKeys
{
    public const string Prefix = "discovery-browse:";

    public static string Create(DiscoverBrowseCriteria criteria, string contentLocale) =>
        ContentLocaleCacheKeySegment.Append(Create(criteria), contentLocale);

    public static string Create(DiscoverBrowseCriteria criteria)
    {
        var effectiveSort = DiscoverBrowseValidator.GetEffectiveSort(criteria);
        var genreFingerprint = CreateGenreFingerprint(criteria.GenreIds);
        var year = criteria.Year?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var yearFrom = criteria.YearFrom?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var yearTo = criteria.YearTo?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var minRating = criteria.MinRating?.ToString("0.##", CultureInfo.InvariantCulture) ?? "none";
        var minVoteCount = criteria.MinVoteCount?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var minRuntime = criteria.MinRuntimeMinutes?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var maxRuntime = criteria.MaxRuntimeMinutes?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var language = string.IsNullOrWhiteSpace(criteria.Language)
            ? "none"
            : criteria.Language.Trim().ToLowerInvariant();
        var originCountry = string.IsNullOrWhiteSpace(criteria.OriginCountry)
            ? "none"
            : criteria.OriginCountry.Trim().ToUpperInvariant();
        var keywordFingerprint = CreateGuidFingerprint(criteria.KeywordIds);
        var tvStatusSegment = criteria.TvStatuses.Count == 0
            ? "none"
            : string.Join('-', criteria.TvStatuses.OrderBy(status => status));

        return $"{Prefix}{criteria.Mode}:{criteria.Type}:{criteria.Page}:{criteria.PageSize}:{genreFingerprint}:{year}:{yearFrom}:{yearTo}:{minRating}:{minVoteCount}:{minRuntime}:{maxRuntime}:{language}:{originCountry}:{keywordFingerprint}:{tvStatusSegment}:{effectiveSort}";
    }

    public static string CreateGenreFingerprint(IReadOnlyList<Guid> genreIds) =>
        CreateGuidFingerprint(genreIds);

    public static string CreateGuidFingerprint(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return "none";
        }

        var joined = string.Join('\u001f', ids.OrderBy(id => id));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }
}
