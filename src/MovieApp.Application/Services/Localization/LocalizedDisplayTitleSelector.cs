using MovieApp.Application.Common;

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
            return ResolveCanonical(canonicalTitle, originalTitle);
        }

        var canonical = ResolveCanonical(canonicalTitle, originalTitle);
        var original = TrimOptional(originalTitle);
        var localized = TrimOptional(localizedTitle);

        return ResolvePrimaryTitle(
            canonical,
            original,
            localized,
            originalLanguage,
            contentLocale,
            primaryOriginCountryCode);
    }

    private static string ResolvePrimaryTitle(
        string canonical,
        string? original,
        string? localized,
        string? originalLanguage,
        string contentLocale,
        string? primaryOriginCountryCode)
    {
        var mode = ClassifyDisplayMode(
            originalLanguage,
            localized,
            canonical,
            contentLocale,
            primaryOriginCountryCode);

        return mode switch
        {
            DisplayTitleMode.SameLanguageProduction => ResolveSameLanguagePrimary(original, localized, canonical),
            DisplayTitleMode.LocaleRegionProduction => localized!,
            _ => FirstNonEmpty(canonical, original, localized),
        };
    }

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

        var canonical = ResolveCanonical(canonicalTitle, originalTitle);
        var localized = TrimOptional(localizedTitle);

        var mode = ClassifyDisplayMode(
            originalLanguage,
            localized,
            canonical,
            contentLocale,
            primaryOriginCountryCode);

        return mode is DisplayTitleMode.SameLanguageProduction or DisplayTitleMode.LocaleRegionProduction
            ? ChooseSubtitleCandidate(primaryTitle, canonical)
            : ChooseSubtitleCandidate(primaryTitle, localized);
    }

    private enum DisplayTitleMode
    {
        SameLanguageProduction,
        LocaleRegionProduction,
        Foreign,
    }

    private static DisplayTitleMode ClassifyDisplayMode(
        string? originalLanguage,
        string? localizedTitle,
        string canonicalTitle,
        string contentLocale,
        string? primaryOriginCountryCode)
    {
        if (!string.IsNullOrWhiteSpace(originalLanguage)
            && ContentLocaleLanguageMatcher.MatchesOriginalLanguage(originalLanguage, contentLocale))
        {
            return DisplayTitleMode.SameLanguageProduction;
        }

        var localized = TrimOptional(localizedTitle);
        if (!string.IsNullOrWhiteSpace(localized)
            && !DisplayTitleEquivalence.AreEquivalent(localized, canonicalTitle)
            && ContentProductionLocaleMatcher.ProductionCountryMatchesRequestedLocale(
                primaryOriginCountryCode,
                contentLocale))
        {
            return DisplayTitleMode.LocaleRegionProduction;
        }

        return DisplayTitleMode.Foreign;
    }

    private static string ResolveSameLanguagePrimary(string? original, string? localized, string canonical)
    {
        if (!string.IsNullOrWhiteSpace(original))
        {
            return original;
        }

        if (!string.IsNullOrWhiteSpace(localized)
            && !DisplayTitleEquivalence.AreEquivalent(localized, canonical))
        {
            return localized;
        }

        return canonical;
    }

    public static (string Title, string? OriginalTitle) ChooseDisplayTitles(
        string canonicalTitle,
        string? originalTitle,
        string? originalLanguage,
        string? localizedTitle,
        string contentLocale,
        string? primaryOriginCountryCode = null)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale))
        {
            return (ResolveCanonical(canonicalTitle, originalTitle), TrimOptional(originalTitle));
        }

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
            : (primary, TrimOptional(originalTitle));
    }

    [Obsolete("Use ContentProductionHeuristics.IsTurkishProduction for poster artwork heuristics.")]
    internal static bool IsTurkishProduction(
        string? originalLanguage,
        string? primaryOriginCountryCode = null,
        string? originalTitle = null) =>
        ContentProductionHeuristics.IsTurkishProduction(
            originalLanguage,
            primaryOriginCountryCode,
            originalTitle);

    private static string? ChooseSubtitleCandidate(string primaryTitle, string? candidate) =>
        string.IsNullOrWhiteSpace(candidate) || DisplayTitleEquivalence.AreEquivalent(primaryTitle, candidate)
            ? null
            : candidate.Trim();

    private static string FirstNonEmpty(string? first, string? second, string? third)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first.Trim();
        }

        if (!string.IsNullOrWhiteSpace(second))
        {
            return second.Trim();
        }

        if (!string.IsNullOrWhiteSpace(third))
        {
            return third.Trim();
        }

        return string.Empty;
    }

    private static string ResolveCanonical(string canonicalTitle, string? originalTitle)
    {
        if (!string.IsNullOrWhiteSpace(canonicalTitle))
        {
            return canonicalTitle.Trim();
        }

        if (!string.IsNullOrWhiteSpace(originalTitle))
        {
            return originalTitle.Trim();
        }

        return string.Empty;
    }

    private static string? TrimOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
