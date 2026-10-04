namespace MovieApp.Application.Services.Localization;

/// <summary>
/// Production heuristics for artwork and legacy call sites — not used for display title policy.
/// </summary>
public static class ContentProductionHeuristics
{
    public static bool IsTurkishProduction(
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

        if (string.Equals(primaryOriginCountryCode?.Trim(), "TR", StringComparison.OrdinalIgnoreCase))
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
}
