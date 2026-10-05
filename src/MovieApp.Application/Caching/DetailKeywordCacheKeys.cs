using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Localization;

namespace MovieApp.Application.Caching;

public static class DetailKeywordCacheKeys
{
    private const string Version = "v1";

    public static string Movie(Guid movieId, string? contentLocale) =>
        $"detail-keywords:movie:{movieId:D}:{NormalizeLocaleKeySegment(contentLocale)}:{Version}";

    public static string TvShow(Guid tvShowId, string? contentLocale) =>
        $"detail-keywords:tv:{tvShowId:D}:{NormalizeLocaleKeySegment(contentLocale)}:{Version}";

    internal static string ResolveContentLocale(string? contentLocale)
    {
        var locale = string.IsNullOrWhiteSpace(contentLocale)
            ? SupportedContentLocales.EnglishUnitedStates
            : contentLocale;

        return KeywordDiscoverLocalizationSupport.NormalizeLocale(locale);
    }

    internal static string NormalizeLocaleKeySegment(string? contentLocale) =>
        ResolveContentLocale(contentLocale).ToLowerInvariant();
}
