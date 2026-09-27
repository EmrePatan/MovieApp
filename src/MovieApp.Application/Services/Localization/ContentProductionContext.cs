namespace MovieApp.Application.Services.Localization;

public readonly record struct ContentProductionContext(
    string? OriginalLanguage,
    string? PrimaryOriginCountryCode = null)
{
    public static ContentProductionContext FromLanguage(string? originalLanguage) =>
        new(originalLanguage);

    public bool IsTurkishProduction(string? originalTitle = null) =>
        LocalizedDisplayTitleSelector.IsTurkishProduction(
            OriginalLanguage,
            PrimaryOriginCountryCode,
            originalTitle);
}
