using MovieApp.Application.Models.Videos;

namespace MovieApp.Application.Services.Videos;

public static class TrailerSelectionService
{
    public static PrimaryVideoResult? SelectPrimary(
        IReadOnlyList<ProviderVideoResult> videos,
        string? contentOriginalLanguage)
    {
        var eligible = videos.Where(IsEligible).ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        var winner = eligible
            .OrderBy(video => GetTypeRank(video.Type))
            .ThenBy(video => video.Official ? 0 : 1)
            .ThenBy(video => GetLanguageRank(video.Language, contentOriginalLanguage))
            .ThenByDescending(video => video.PublishedAt ?? DateTimeOffset.MinValue)
            .ThenBy(video => video.Key, StringComparer.Ordinal)
            .First();

        return new PrimaryVideoResult(
            Site: "YouTube",
            Type: NormalizeType(winner.Type),
            Name: winner.Name,
            Language: NormalizeLanguage(winner.Language),
            Official: winner.Official,
            WatchUrl: YouTubeWatchUrlBuilder.BuildWatchUrl(winner.Key));
    }

    private static bool IsEligible(ProviderVideoResult video)
    {
        if (!string.Equals(video.Site, "YouTube", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!YouTubeKeyValidator.IsValid(video.Key))
        {
            return false;
        }

        var type = video.Type?.Trim() ?? string.Empty;
        return string.Equals(type, "Trailer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "Teaser", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetTypeRank(string? type)
    {
        if (string.Equals(type?.Trim(), "Trailer", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return 1;
    }

    private static int GetLanguageRank(string? videoLanguage, string? contentOriginalLanguage)
    {
        var normalizedVideoLanguage = NormalizeLanguage(videoLanguage);
        var normalizedContentLanguage = NormalizeLanguage(contentOriginalLanguage);

        if (normalizedVideoLanguage is not null
            && normalizedContentLanguage is not null
            && string.Equals(normalizedVideoLanguage, normalizedContentLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(normalizedVideoLanguage, "en", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static string NormalizeType(string? type)
    {
        if (string.Equals(type?.Trim(), "Teaser", StringComparison.OrdinalIgnoreCase))
        {
            return "Teaser";
        }

        return "Trailer";
    }

    private static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        return language.Trim().ToLowerInvariant();
    }
}
