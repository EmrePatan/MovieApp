namespace MovieApp.Application.Services.Localization;

public static class ContentLocaleResolver
{
    public const string EnglishUnitedStates = "en-US";
    public const string TurkishTurkey = "tr-TR";

    public static string ResolveFromAcceptLanguage(string? acceptLanguageHeader)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguageHeader))
        {
            return EnglishUnitedStates;
        }

        var firstLanguage = acceptLanguageHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => segment.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(firstLanguage))
        {
            return EnglishUnitedStates;
        }

        if (firstLanguage.Trim().StartsWith("tr", StringComparison.OrdinalIgnoreCase))
        {
            return TurkishTurkey;
        }

        return EnglishUnitedStates;
    }

    public static bool RequiresLocalization(string contentLocale) =>
        !string.Equals(contentLocale, EnglishUnitedStates, StringComparison.OrdinalIgnoreCase);
}
