namespace MovieApp.Application.Services.Localization;

public readonly record struct ContentProductionContext(
    string? OriginalLanguage,
    string? PrimaryOriginCountryCode = null)
{
    public static ContentProductionContext FromLanguage(string? originalLanguage) =>
        new(originalLanguage);

    public bool IsTurkishProduction(string? originalTitle = null) =>
        ContentProductionHeuristics.IsTurkishProduction(
            OriginalLanguage,
            PrimaryOriginCountryCode,
            originalTitle);

    public bool MatchesRequestedLocaleRegion(string contentLocale) =>
        ContentProductionLocaleMatcher.ProductionCountryMatchesRequestedLocale(
            PrimaryOriginCountryCode,
            contentLocale);
}
