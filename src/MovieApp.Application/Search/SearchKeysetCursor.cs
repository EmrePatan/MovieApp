using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MovieApp.Application.Common;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Search;

public sealed class SearchKeysetCursor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Version { get; init; } = 2;

    public int Page { get; init; }

    public string Fingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Exact total from the first page of this cursor chain (snapshot; not recalculated on continuation).
    /// </summary>
    public int SnapshotTotalCount { get; init; }

    public int RelevanceTier { get; init; }

    public decimal VoteAverage { get; init; }

    public int VoteCount { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? ReleaseDateIso { get; init; }

    public string Type { get; init; } = string.Empty;

    public Guid Id { get; init; }

    public static string ComputeFingerprint(SearchCriteria criteria, string? normalizedQuery)
    {
        var builder = new StringBuilder();
        builder.Append((int)criteria.Type).Append('|');
        builder.Append(normalizedQuery ?? string.Empty).Append('|');
        builder.Append(criteria.GenreId?.ToString() ?? string.Empty).Append('|');
        builder.Append(criteria.Year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append('|');
        builder.Append(criteria.MinRating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append('|');
        builder.Append(criteria.MaxRating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append('|');
        builder.Append((int)criteria.Sort).Append('|');
        builder.Append(criteria.PageSize.ToString(CultureInfo.InvariantCulture));
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public static SearchKeysetCursor CreateFromItem(
        SearchItem item,
        SearchCriteria criteria,
        string? normalizedQuery,
        int page,
        int snapshotTotalCount)
    {
        return new SearchKeysetCursor
        {
            Page = page,
            Fingerprint = ComputeFingerprint(criteria, normalizedQuery),
            SnapshotTotalCount = snapshotTotalCount,
            RelevanceTier = ComputeRelevanceTier(item, normalizedQuery),
            VoteAverage = item.VoteAverage,
            VoteCount = item.VoteCount,
            Title = item.Title,
            ReleaseDateIso = item.ReleaseDate?.ToString("O", CultureInfo.InvariantCulture),
            Type = item.Type,
            Id = item.Id
        };
    }

    public static int ComputeRelevanceTier(SearchItem item, string? normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return 0;
        }

        return SearchTitleMatching.ComputeRelevanceTier(
            item.Title,
            new SearchTextMatch(normalizedQuery, SearchTurkishCaseFolder.TryCreateAlternate(normalizedQuery)));
    }

    public static string Encode(SearchKeysetCursor cursor)
    {
        var json = JsonSerializer.Serialize(cursor, JsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static bool TryDecode(
        string? encoded,
        SearchCriteria criteria,
        string? normalizedQuery,
        out SearchKeysetCursor? cursor,
        out string? errorMessage)
    {
        cursor = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(encoded))
        {
            errorMessage = "Cursor is required.";
            return false;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Trim()));
            var parsed = JsonSerializer.Deserialize<SearchKeysetCursor>(json, JsonOptions);
            if (parsed is null || parsed.Id == Guid.Empty || parsed.Version is not (1 or 2))
            {
                errorMessage = "Cursor is invalid.";
                return false;
            }

            if (parsed.Version == 2 && parsed.SnapshotTotalCount < 0)
            {
                errorMessage = "Cursor is invalid.";
                return false;
            }

            var expectedFingerprint = ComputeFingerprint(criteria, normalizedQuery);
            if (!string.Equals(parsed.Fingerprint, expectedFingerprint, StringComparison.Ordinal))
            {
                errorMessage = "Cursor does not match the current search request.";
                return false;
            }

            if (parsed.Page < 1)
            {
                errorMessage = "Cursor is invalid.";
                return false;
            }

            cursor = parsed;
            return true;
        }
        catch (FormatException)
        {
            errorMessage = "Cursor is invalid.";
            return false;
        }
        catch (JsonException)
        {
            errorMessage = "Cursor is invalid.";
            return false;
        }
    }
}
