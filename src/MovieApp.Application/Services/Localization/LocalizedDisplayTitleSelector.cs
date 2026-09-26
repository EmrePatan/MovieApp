namespace MovieApp.Application.Services.Localization;

public static class LocalizedDisplayTitleSelector
{
    public static string Choose(
        string canonicalTitle,
        string? originalTitle,
        string? originalLanguage,
        string? localizedTitle,
        string contentLocale)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return TrimRequired(canonicalTitle);
        }

        var canonical = TrimRequired(canonicalTitle);
        var original = TrimOptional(originalTitle);
        var localized = TrimOptional(localizedTitle);

        if (!string.IsNullOrEmpty(localized) &&
            !string.Equals(localized, canonical, StringComparison.OrdinalIgnoreCase))
        {
            return localized;
        }

        if (ShouldPreferOriginalTitle(canonical, original, localized, originalLanguage, contentLocale))
        {
            return original!;
        }

        if (!string.IsNullOrEmpty(localized))
        {
            return localized;
        }

        if (!string.IsNullOrEmpty(canonical))
        {
            return canonical;
        }

        return original ?? string.Empty;
    }

    private static bool ShouldPreferOriginalTitle(
        string canonical,
        string? original,
        string? localized,
        string? originalLanguage,
        string contentLocale)
    {
        if (string.IsNullOrEmpty(original))
        {
            return false;
        }

        if (string.Equals(original, canonical, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var localizedIneffective = string.IsNullOrEmpty(localized) ||
            string.Equals(localized, canonical, StringComparison.OrdinalIgnoreCase);

        if (!localizedIneffective)
        {
            return false;
        }

        if (ContentLocaleLanguageMatcher.MatchesOriginalLanguage(originalLanguage, contentLocale))
        {
            return true;
        }

        return originalLanguage is null;
    }

    private static string TrimRequired(string value) => value.Trim();

    private static string? TrimOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
