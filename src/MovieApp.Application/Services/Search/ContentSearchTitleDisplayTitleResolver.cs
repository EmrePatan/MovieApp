using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Services.Search;

public static class ContentSearchTitleDisplayTitleResolver
{
    public static IReadOnlyDictionary<CatalogContentKey, string> BuildDisplayTitlesByContentKey(
        IEnumerable<ContentSearchTitle> rows,
        CatalogSearchTitleLanguageScope scope)
    {
        var result = new Dictionary<CatalogContentKey, string>();
        foreach (var group in rows.GroupBy(row => (row.ContentId, row.ContentType)))
        {
            var typeLabel = group.Key.ContentType == CatalogContentType.Movie ? "movie" : "tv";
            var best = group
                .Where(row => RowMatchesLocaleDisplay(row, scope))
                .OrderByDescending(ScoreLocaleDisplayRow)
                .FirstOrDefault();

            if (best is not null)
            {
                result[new CatalogContentKey(group.Key.ContentId, typeLabel)] = best.Title;
            }
        }

        return result;
    }

    public static IReadOnlyDictionary<CatalogContentKey, string> BuildMovieDisplayTitles(
        IEnumerable<ContentSearchTitle> rows,
        CatalogSearchTitleLanguageScope scope) =>
        BuildDisplayTitlesByContentKey(
            rows.Where(row => row.ContentType == CatalogContentType.Movie),
            scope);

    public static IReadOnlyDictionary<CatalogContentKey, string> BuildTvDisplayTitles(
        IEnumerable<ContentSearchTitle> rows,
        CatalogSearchTitleLanguageScope scope) =>
        BuildDisplayTitlesByContentKey(
            rows.Where(row => row.ContentType == CatalogContentType.Tv),
            scope);

    private static bool RowMatchesLocaleDisplay(
        ContentSearchTitle row,
        CatalogSearchTitleLanguageScope scope)
    {
        if (row.TitleKind == ContentSearchTitleKind.Translation
            && row.LanguageCode is { Length: > 0 } languageCode
            && string.Equals(languageCode, scope.PrimaryLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (row.TitleKind == ContentSearchTitleKind.Alternative
            && scope.PrimaryRegionCode is { Length: > 0 } regionCode
            && row.CountryCode is { Length: > 0 } countryCode
            && string.Equals(countryCode, regionCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static int ScoreLocaleDisplayRow(ContentSearchTitle row) =>
        row.TitleKind == ContentSearchTitleKind.Translation ? 2 : 1;
}
