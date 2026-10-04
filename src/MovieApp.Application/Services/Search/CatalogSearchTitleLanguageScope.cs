using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Services.Search;

/// <summary>
/// Limits which <c>content_search_titles</c> rows participate in catalog search for a content locale.
/// </summary>
public sealed record CatalogSearchTitleLanguageScope(
    string PrimaryLanguageCode,
    string? PrimaryRegionCode,
    bool IncludeEnglishLanguage)
{
    public const string EnglishLanguageCode = "en";

    public static CatalogSearchTitleLanguageScope FromContentLocale(string contentLocale)
    {
        var normalized = ContentLocaleResolver.Normalize(contentLocale);
        var segments = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var primaryLanguage = segments[0].ToLowerInvariant();
        string? primaryRegion = segments.Length > 1
            ? segments[^1].ToUpperInvariant()
            : null;

        var includeEnglish = !string.Equals(primaryLanguage, EnglishLanguageCode, StringComparison.Ordinal);

        return new CatalogSearchTitleLanguageScope(primaryLanguage, primaryRegion, includeEnglish);
    }
}
