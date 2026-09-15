using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Caching;

public static class DiscoveryBrowseCacheKeys
{
    public const string Prefix = "discovery-browse:";

    public static string Create(DiscoverBrowseCriteria criteria)
    {
        var effectiveSort = DiscoverBrowseValidator.GetEffectiveSort(criteria);
        var genreFingerprint = CreateGenreFingerprint(criteria.GenreIds);
        var year = criteria.Year?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var minRating = criteria.MinRating?.ToString("0.##", CultureInfo.InvariantCulture) ?? "none";
        var language = string.IsNullOrWhiteSpace(criteria.Language)
            ? "none"
            : criteria.Language.Trim().ToLowerInvariant();

        return $"{Prefix}{criteria.Mode}:{criteria.Type}:{criteria.Page}:{criteria.PageSize}:{genreFingerprint}:{year}:{minRating}:{language}:{effectiveSort}";
    }

    public static string CreateGenreFingerprint(IReadOnlyList<Guid> genreIds)
    {
        if (genreIds.Count == 0)
        {
            return "none";
        }

        var joined = string.Join('\u001f', genreIds.OrderBy(id => id));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant();
    }
}
