using MovieApp.Application.Services.Images;

namespace MovieApp.Application.Services.Localization;

public static class SupportedArtworkLanguageKeys
{
    public const string Turkish = "tr";

    private static readonly string[] EnrichmentLanguageKeys = [Turkish];

    public static IReadOnlyList<string> ForProviderEnrichment => EnrichmentLanguageKeys;

    public static string BuildTmdbIncludeImageLanguageParameter()
    {
        var parts = new List<string>(EnrichmentLanguageKeys.Length + 1);
        parts.AddRange(EnrichmentLanguageKeys);
        parts.Add("null");
        return string.Join(',', parts);
    }

    public static string? ResolvePosterLanguageKey(string contentLocale)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return null;
        }

        var normalizedLocale = ContentLocaleResolver.Normalize(contentLocale);
        return normalizedLocale switch
        {
            ContentLocaleResolver.TurkishTurkey => Turkish,
            _ => ImageGalleryServiceHelper.NormalizeLanguage(normalizedLocale)
        };
    }
}
