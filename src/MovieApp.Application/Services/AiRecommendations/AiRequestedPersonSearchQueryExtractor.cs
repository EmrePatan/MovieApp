using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MovieApp.Application.Services.AiRecommendations;

internal static partial class AiRequestedPersonSearchQueryExtractor
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] StrongPersonIntentMarkers =
    [
        "oynadığı",
        "oyunculuğu",
        "oyunculuğunu",
        "başrolünde",
        "başrolü",
        "filmleri",
        "dizileri",
        "yapımları",
        "starring",
        "movies with",
        "films with",
        "series with",
        "tv shows with"
    ];

    private static readonly HashSet<string> GenreOrTopicTokens = new(StringComparer.Ordinal)
    {
        "korku",
        "komedi",
        "aksiyon",
        "drama",
        "romantik",
        "bilim",
        "bilimkurgu",
        "animasyon",
        "belgesel",
        "gerilim",
        "macera",
        "fantastik",
        "suç",
        "gizem",
        "aile",
        "cocuk",
        "çocuk",
        "savaş",
        "tarih",
        "muzikal",
        "müzikal",
        "anime",
        "populer",
        "popüler",
        "iyi",
        "guzel",
        "güzel",
        "yeni",
        "eski",
        "en"
    };

    internal static string? TryExtract(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return null;
        }

        var trimmed = userMessage.Trim();
        if (!HasPersonIntent(trimmed))
        {
            return null;
        }

        var query = ExtractLeadingName(trimmed);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        query = CleanupQuery(query);
        if (query.Length < 2 || query.Length > 80 || IsGenreOrTopicQuery(query))
        {
            return null;
        }

        return query;
    }

    private static bool HasPersonIntent(string message)
    {
        var lower = message.ToLower(TurkishCulture);
        foreach (var marker in StrongPersonIntentMarkers)
        {
            if (lower.Contains(marker, StringComparison.Ordinal))
            {
                return true;
            }
        }

        var beforeRecommend = RecommendTailRegex().Split(message)[0].Trim();
        if (!LeadingNameRegex().IsMatch(beforeRecommend))
        {
            return false;
        }

        var query = ExtractLeadingName(message);
        return !string.IsNullOrWhiteSpace(query) && !IsGenreOrTopicQuery(query);
    }

    private static string ExtractLeadingName(string message)
    {
        var beforeRecommend = RecommendTailRegex().Split(message)[0].Trim();
        var match = LeadingNameRegex().Match(beforeRecommend);
        if (match.Success)
        {
            return match.Groups["name"].Value.Trim();
        }

        return beforeRecommend;
    }

    private static string CleanupQuery(string query)
    {
        var cleaned = query.Trim();
        cleaned = FillerWordsRegex().Replace(cleaned, " ").Trim();
        cleaned = TrailingFilmDiziRegex().Replace(cleaned, string.Empty).Trim();
        cleaned = LeadingPolitenessRegex().Replace(cleaned, string.Empty).Trim();
        return cleaned;
    }

    private static bool IsGenreOrTopicQuery(string query)
    {
        var tokens = query.ToLower(TurkishCulture)
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            return true;
        }

        if (tokens.Length == 1)
        {
            return GenreOrTopicTokens.Contains(tokens[0]);
        }

        return tokens.All(token => GenreOrTopicTokens.Contains(token));
    }

    [GeneratedRegex(@"\b(öner|oner|recommend|suggest)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecommendTailRegex();

    [GeneratedRegex(
        @"^(?<name>.+?)\s+(?:bir\s+)?(?:film|dizi|filmi|filmini|dizisi|dizisini|yapım|yapımı)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNameRegex();

    [GeneratedRegex(@"\b(veya|veya da|and|or|bi|bir|some|any|lütfen|please)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FillerWordsRegex();

    [GeneratedRegex(
        @"\s+(?:bir\s+)?(?:film|dizi|filmi|filmini|dizisi|dizisini|yapım|yapımı).*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingFilmDiziRegex();

    [GeneratedRegex(@"^(?:bana|benim için|please)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingPolitenessRegex();
}
