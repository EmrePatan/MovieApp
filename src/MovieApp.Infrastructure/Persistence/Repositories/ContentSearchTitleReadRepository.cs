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

        return contentType == CatalogContentType.Movie
            ? ContentSearchTitleDisplayTitleResolver.BuildMovieDisplayTitles(rows, scope)
            : ContentSearchTitleDisplayTitleResolver.BuildTvDisplayTitles(rows, scope);
    }
}
