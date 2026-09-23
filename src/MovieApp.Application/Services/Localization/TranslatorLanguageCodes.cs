namespace MovieApp.Application.Services.Localization;

public static class TranslatorLanguageCodes
{
    public static string FromContentLocale(string contentLocale) =>
        SupportedContentLocales.Normalize(contentLocale) switch
        {
            SupportedContentLocales.TurkishTurkey => "tr",
            SupportedContentLocales.SpanishSpain => "es",
            SupportedContentLocales.GermanGermany => "de",
            SupportedContentLocales.FrenchFrance => "fr",
            SupportedContentLocales.ItalianItaly => "it",
            SupportedContentLocales.PortugueseBrazil => "pt",
            _ => "en",
        };

    public static bool SourceMatchesTarget(string detectedSourceLanguage, string targetContentLocale)
    {
        if (string.IsNullOrWhiteSpace(detectedSourceLanguage))
        {
            return false;
        }

        var detected = detectedSourceLanguage
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0]
            .ToLowerInvariant();

        var target = FromContentLocale(targetContentLocale);
        return string.Equals(detected, target, StringComparison.Ordinal);
    }
}
