namespace MovieApp.Application.Services.Localization;

public static class LocalizedDisplayTitleSelector
{
    public static string Choose(
        string canonicalTitle,
        string? originalTitle,
        string? originalLanguage,
        string? localizedTitle,
        string contentLocale) =>
        ChoosePrimary(canonicalTitle, originalTitle, originalLanguage, localizedTitle, contentLocale);

    public static string ChoosePrimary(
        string canonicalTitle,
        string? originalTitle,
        string? originalLanguage,
        string? localizedTitle,
        string contentLocale,
        string? primaryOriginCountryCode = null)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return TrimRequired(canonicalTitle);
        }

        var canonical = TrimRequired(canonicalTitle);
        var original = TrimOptional(originalTitle);
        var localized = TrimOptional(localizedTitle);

        if (IsTurkishProduction(originalLanguage, primaryOriginCountryCode, original))
        {
            if (!string.IsNullOrEmpty(localized) &&
                !string.Equals(localized, canonical, StringComparison.OrdinalIgnoreCase))
            {
                return localized;
            }

            if (!string.IsNullOrEmpty(original))
            {
                return original;
            }

            return canonical;
        }

        if (ContentLocaleLanguageMatcher.MatchesOriginalLanguage(
                originalLanguage,
                ContentLocaleResolver.EnglishUnitedStates))
        {
            return canonical;
        }

        if (!string.IsNullOrEmpty(original))
        {
            return original;
        }

        return canonical;
    }

    /// <summary>
    /// Secondary line under the primary title in tr-TR: export English for Turkish productions,
    /// TMDB Turkish title for imported titles when available.
    /// </summary>
    public static string? ChooseSubtitle(
        string primaryTitle,
        string canonicalTitle,
        string? originalLanguage,
        string? localizedTitle,
        string contentLocale,
        string? primaryOriginCountryCode = null,
        string? originalTitle = null)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return null;
        }

        if (IsTurkishProduction(originalLanguage, primaryOriginCountryCode, originalTitle))
        {
            var exportTitle = TrimOptional(canonicalTitle);
            if (string.IsNullOrEmpty(exportTitle) ||
                string.Equals(exportTitle, primaryTitle, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return exportTitle;
        }

        var turkishTitle = TrimOptional(localizedTitle);
        if (string.IsNullOrEmpty(turkishTitle) ||
            string.Equals(turkishTitle, primaryTitle, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return turkishTitle;
    }

    public static (string Title, string? OriginalTitle) ChooseDisplayTitles(
        string canonicalTitle,
        string? originalTitle,
        string? originalLanguage,
        string? localizedTitle,
        string contentLocale,
        string? primaryOriginCountryCode = null)
    {
        var primary = ChoosePrimary(
            canonicalTitle,
            originalTitle,
            originalLanguage,
            localizedTitle,
            contentLocale,
            primaryOriginCountryCode);
        var subtitle = ChooseSubtitle(
            primary,
            canonicalTitle,
            originalLanguage,
            localizedTitle,
            contentLocale,
            primaryOriginCountryCode,
            originalTitle);

        return subtitle is not null
            ? (primary, subtitle)
            : (primary, originalTitle);
    }

    internal static bool IsTurkishProduction(
        string? originalLanguage,
        string? primaryOriginCountryCode = null,
        string? originalTitle = null)
    {
        if (ContentLocaleLanguageMatcher.MatchesOriginalLanguage(
                originalLanguage,
                ContentLocaleResolver.TurkishTurkey))
        {
            return true;
        }

        if (IsTurkeyOriginCountry(primaryOriginCountryCode))
        {
            return true;
        }

        if (ContainsTurkishScript(originalTitle) &&
            !ContentLocaleLanguageMatcher.MatchesOriginalLanguage(
                originalLanguage,
                ContentLocaleResolver.EnglishUnitedStates))
        {
            return true;
        }

        return false;
    }

    private static bool IsTurkeyOriginCountry(string? primaryOriginCountryCode) =>
        string.Equals(primaryOriginCountryCode?.Trim(), "TR", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsTurkishScript(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            switch (character)
            {
                case 'ç' or 'Ç' or 'ğ' or 'Ğ' or 'ı' or 'İ' or 'ö' or 'Ö' or 'ş' or 'Ş' or 'ü' or 'Ü':
                    return true;
            }
        }

        return false;
    }

    private static string TrimRequired(string value) => value.Trim();

    private static string? TrimOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
