namespace MovieApp.Application.Services.Search;

/// <summary>
/// Adjusts alias match tiers so unified search ranks locale, then English, then other aliases
/// without excluding any indexed title from eligibility.
/// </summary>
public static class SearchAliasLocaleRanking
{
    /// <summary>Shifts direct alias tiers (6–8) into the canonical band (0–2).</summary>
    public const int RequestedLocaleDirectPromotion = -6;

    /// <summary>Shifts direct alias tiers (6–8) into the original-title band (3–5).</summary>
    public const int EnglishDirectPromotion = -3;

    /// <summary>Shifts folded alias tiers (15–17) into the folded canonical band (9–11).</summary>
    public const int RequestedLocaleFoldedPromotion = -6;

    /// <summary>Shifts folded alias tiers (15–17) into the folded original band (12–14).</summary>
    public const int EnglishFoldedPromotion = -3;

    public static bool MatchesRequestedLocale(
        string? languageCode,
        string? countryCode,
        CatalogSearchTitleLanguageScope scope)
    {
        if (!string.IsNullOrEmpty(languageCode)
            && string.Equals(languageCode, scope.PrimaryLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (string.IsNullOrEmpty(languageCode) || languageCode == string.Empty)
            && !string.IsNullOrEmpty(countryCode)
            && scope.PrimaryRegionCode is { Length: > 0 } regionCode
            && string.Equals(countryCode, regionCode, StringComparison.OrdinalIgnoreCase);
    }

    public static bool MatchesEnglish(string? languageCode, CatalogSearchTitleLanguageScope scope) =>
        scope.IncludeEnglishLanguage
        && !string.IsNullOrEmpty(languageCode)
        && string.Equals(languageCode, CatalogSearchTitleLanguageScope.EnglishLanguageCode, StringComparison.OrdinalIgnoreCase);
}
