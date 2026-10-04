namespace MovieApp.Application.Services.Catalog;

internal static class CatalogProviderGenreNames
{
    public static bool ContainsUsableGenreNames(IReadOnlyList<string>? genreNames) =>
        genreNames is not null &&
        genreNames.Any(name => !string.IsNullOrWhiteSpace(name));
}
