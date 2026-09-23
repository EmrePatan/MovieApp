namespace MovieApp.Application.Services.Localization;

public static class ContentLocaleResolver
{
    public const string EnglishUnitedStates = SupportedContentLocales.EnglishUnitedStates;
    public const string TurkishTurkey = SupportedContentLocales.TurkishTurkey;
    public const string SpanishSpain = SupportedContentLocales.SpanishSpain;
    public const string GermanGermany = SupportedContentLocales.GermanGermany;
    public const string FrenchFrance = SupportedContentLocales.FrenchFrance;
    public const string ItalianItaly = SupportedContentLocales.ItalianItaly;
    public const string PortugueseBrazil = SupportedContentLocales.PortugueseBrazil;

    public static string ResolveFromAcceptLanguage(string? acceptLanguageHeader) =>
        SupportedContentLocales.ResolveFromAcceptLanguage(acceptLanguageHeader);

    public static string Normalize(string? locale) =>
        SupportedContentLocales.Normalize(locale);

    public static bool RequiresLocalization(string contentLocale) =>
        SupportedContentLocales.RequiresLocalization(contentLocale);
}
