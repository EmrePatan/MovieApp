namespace MovieApp.Application.Services.Localization;

public static class SupportedContentLocales
{
    public const string EnglishUnitedStates = "en-US";
    public const string TurkishTurkey = "tr-TR";
    public const string SpanishSpain = "es-ES";
    public const string GermanGermany = "de-DE";
    public const string FrenchFrance = "fr-FR";
    public const string ItalianItaly = "it-IT";
    public const string PortugueseBrazil = "pt-BR";
    public const string Default = EnglishUnitedStates;

    private static readonly string[] All =
    [
        EnglishUnitedStates,
        TurkishTurkey,
        SpanishSpain,
        GermanGermany,
        FrenchFrance,
        ItalianItaly,
        PortugueseBrazil,
    ];

    private static readonly Dictionary<string, string> ExactLocaleMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = EnglishUnitedStates,
            ["en-us"] = EnglishUnitedStates,
            [EnglishUnitedStates] = EnglishUnitedStates,
            ["tr"] = TurkishTurkey,
            ["tr-tr"] = TurkishTurkey,
            [TurkishTurkey] = TurkishTurkey,
            ["es"] = SpanishSpain,
            ["es-es"] = SpanishSpain,
            [SpanishSpain] = SpanishSpain,
            ["de"] = GermanGermany,
            ["de-de"] = GermanGermany,
            [GermanGermany] = GermanGermany,
            ["fr"] = FrenchFrance,
            ["fr-fr"] = FrenchFrance,
            [FrenchFrance] = FrenchFrance,
            ["it"] = ItalianItaly,
            ["it-it"] = ItalianItaly,
            [ItalianItaly] = ItalianItaly,
            ["pt"] = PortugueseBrazil,
            ["pt-br"] = PortugueseBrazil,
            [PortugueseBrazil] = PortugueseBrazil,
        };

    public static IReadOnlyList<string> SupportedLocales => All;

    public static string Normalize(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return Default;
        }

        var trimmed = locale.Trim();
        if (ExactLocaleMap.TryGetValue(trimmed, out var exact))
        {
            return exact;
        }

        var languagePrefix = trimmed.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(languagePrefix) &&
            ExactLocaleMap.TryGetValue(languagePrefix, out var prefixMatch))
        {
            return prefixMatch;
        }

        return Default;
    }

    public static string ResolveFromAcceptLanguage(string? acceptLanguageHeader)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguageHeader))
        {
            return Default;
        }

        var candidates = acceptLanguageHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseLanguageRange)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Language))
            .OrderByDescending(candidate => candidate.Quality)
            .ToList();

        foreach (var candidate in candidates)
        {
            var normalized = Normalize(candidate.Language);
            if (IsSupported(normalized))
            {
                return normalized;
            }
        }

        return Default;
    }

    public static bool IsSupported(string contentLocale) =>
        All.Contains(contentLocale, StringComparer.OrdinalIgnoreCase);

    public static bool RequiresLocalization(string contentLocale) =>
        !string.Equals(Normalize(contentLocale), EnglishUnitedStates, StringComparison.OrdinalIgnoreCase);

    private static (string Language, double Quality) ParseLanguageRange(string segment)
    {
        var parts = segment.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var language = parts[0];
        var quality = 1.0d;

        for (var index = 1; index < parts.Length; index++)
        {
            var parameter = parts[index];
            if (!parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = parameter[2..];
            if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            {
                quality = parsed;
            }
        }

        return (language, quality);
    }
}
