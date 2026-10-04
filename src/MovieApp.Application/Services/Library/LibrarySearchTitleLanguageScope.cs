using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.Library;

/// <summary>
/// Library alias for <see cref="CatalogSearchTitleLanguageScope"/>.
/// </summary>
public sealed record LibrarySearchTitleLanguageScope(
    string PrimaryLanguageCode,
    string? PrimaryRegionCode,
    bool IncludeEnglishLanguage)
{
    public const string EnglishLanguageCode = CatalogSearchTitleLanguageScope.EnglishLanguageCode;

    public static LibrarySearchTitleLanguageScope FromContentLocale(string contentLocale)
    {
        var scope = CatalogSearchTitleLanguageScope.FromContentLocale(contentLocale);
        return new LibrarySearchTitleLanguageScope(
            scope.PrimaryLanguageCode,
            scope.PrimaryRegionCode,
            scope.IncludeEnglishLanguage);
    }
}
