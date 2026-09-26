using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Library;

public sealed class LibraryKeysetCursor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Version { get; init; } = 1;

    public int Page { get; init; }

    public string Fingerprint { get; init; } = string.Empty;

    public int SnapshotTotalCount { get; init; }

    public bool WatchingInProgress { get; init; }

    public string SortInstantUtc { get; init; } = string.Empty;

    public string? Type { get; init; }

    public Guid PrimaryId { get; init; }

    public static string ComputeFingerprint(
        Guid userId,
        LibraryCategory category,
        SearchContentType mediaType,
        int pageSize)
    {
        var builder = new StringBuilder();
        builder.Append(userId.ToString("D")).Append('|');
        builder.Append((int)category).Append('|');
        builder.Append((int)mediaType).Append('|');
        builder.Append(pageSize.ToString(CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash);
    }

    public static string Encode(LibraryKeysetCursor cursor)
    {
        var json = JsonSerializer.Serialize(cursor, JsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static bool TryDecode(
        string? encoded,
        Guid userId,
        LibraryCriteria criteria,
        out LibraryKeysetCursor? cursor,
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
            var parsed = JsonSerializer.Deserialize<LibraryKeysetCursor>(json, JsonOptions);
            if (parsed is null || parsed.Version != 1 || parsed.PrimaryId == Guid.Empty)
            {
                errorMessage = "Cursor is invalid.";
                return false;
            }

            if (parsed.SnapshotTotalCount < 0)
            {
                errorMessage = "Cursor is invalid.";
                return false;
            }

            var expectedFingerprint = ComputeFingerprint(
                userId,
                criteria.Category,
                criteria.MediaType,
                criteria.PageSize);
            if (!string.Equals(parsed.Fingerprint, expectedFingerprint, StringComparison.Ordinal))
            {
                errorMessage = "Cursor does not match the current library request.";
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

    public static LibraryKeysetCursor CreateWatchingAnchor(
        Guid userId,
        LibraryCriteria criteria,
        int page,
        int snapshotTotalCount,
        bool inProgress,
        DateTime lastWatchedAt,
        Guid tvShowId) =>
        CreateAnchor(userId, criteria, page, snapshotTotalCount, inProgress, lastWatchedAt, null, tvShowId);

    public static LibraryKeysetCursor CreateDateAnchor(
        Guid userId,
        LibraryCriteria criteria,
        int page,
        int snapshotTotalCount,
        DateTime sortInstant,
        string? type,
        Guid primaryId) =>
        CreateAnchor(userId, criteria, page, snapshotTotalCount, false, sortInstant, type, primaryId);

    private static LibraryKeysetCursor CreateAnchor(
        Guid userId,
        LibraryCriteria criteria,
        int page,
        int snapshotTotalCount,
        bool watchingInProgress,
        DateTime sortInstant,
        string? type,
        Guid primaryId) =>
        new()
        {
            Page = page,
            Fingerprint = ComputeFingerprint(userId, criteria.Category, criteria.MediaType, criteria.PageSize),
            SnapshotTotalCount = snapshotTotalCount,
            WatchingInProgress = watchingInProgress,
            SortInstantUtc = sortInstant.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            Type = type,
            PrimaryId = primaryId
        };

    public DateTime GetSortInstant() =>
        DateTime.Parse(SortInstantUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
