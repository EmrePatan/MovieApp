using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Library;

/// <summary>
/// Limits indexed provider titles used by library <c>q</c> search to the user's content locale plus English.
/// </summary>
public sealed record LibrarySearchTitleLanguageScope(
    string PrimaryLanguageCode,
    string? PrimaryRegionCode,
    bool IncludeEnglishLanguage)
{
    public const string EnglishLanguageCode = "en";

    public static LibrarySearchTitleLanguageScope FromContentLocale(string contentLocale)
    {
        var normalized = ContentLocaleResolver.Normalize(contentLocale);
        var segments = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var primaryLanguage = segments[0].ToLowerInvariant();
        string? primaryRegion = segments.Length > 1
            ? segments[^1].ToUpperInvariant()
            : null;

        var includeEnglish = !string.Equals(primaryLanguage, EnglishLanguageCode, StringComparison.Ordinal);

        return new LibrarySearchTitleLanguageScope(primaryLanguage, primaryRegion, includeEnglish);
    }
}
