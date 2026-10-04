using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ContentSearchTitleReadRepository(ApplicationDbContext dbContext) : IContentSearchTitleReadRepository
{
    public async Task<IReadOnlyDictionary<CatalogContentKey, string>> GetLocaleDisplayTitlesAsync(
        CatalogContentType contentType,
        IReadOnlyList<Guid> contentIds,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (contentIds.Count == 0)
        {
            return new Dictionary<CatalogContentKey, string>();
        }

        var scope = CatalogSearchTitleLanguageScope.FromContentLocale(contentLocale);
        var rows = await dbContext.ContentSearchTitles
            .AsNoTracking()
            .Where(row =>
                row.ContentType == contentType
                && contentIds.Contains(row.ContentId)
                && (row.TitleKind == ContentSearchTitleKind.Translation
                    || row.TitleKind == ContentSearchTitleKind.Alternative))
            .ToListAsync(cancellationToken);

        var typeLabel = contentType == CatalogContentType.Movie ? "movie" : "tv";
        var result = new Dictionary<CatalogContentKey, string>();

        foreach (var group in rows.GroupBy(row => row.ContentId))
        {
            var best = group
                .Where(row => RowMatchesLocaleDisplay(row, scope))
                .OrderByDescending(ScoreLocaleDisplayRow)
                .FirstOrDefault();

            if (best is not null)
            {
                result[new CatalogContentKey(group.Key, typeLabel)] = best.Title;
            }
        }

        return result;
    }

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
