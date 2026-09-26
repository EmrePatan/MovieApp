using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ContentLocalizedPosterRepository(ApplicationDbContext dbContext)
    : IContentLocalizedPosterRepository
{
    public async Task<IReadOnlyDictionary<ContentLocalizedPosterKey, string>> GetPosterPathsAsync(
        IReadOnlyList<ContentLocalizedPosterKey> keys,
        string languageKey,
        CancellationToken cancellationToken = default)
    {
        if (keys.Count == 0)
        {
            return new Dictionary<ContentLocalizedPosterKey, string>();
        }

        var normalizedLanguage = languageKey.Trim().ToLowerInvariant();
        var movieIds = keys
            .Where(key => key.ContentType == CatalogContentType.Movie)
            .Select(key => key.ContentId)
            .Distinct()
            .ToList();
        var tvIds = keys
            .Where(key => key.ContentType == CatalogContentType.Tv)
            .Select(key => key.ContentId)
            .Distinct()
            .ToList();

        var rows = await dbContext.Set<ContentLocalizedPoster>()
            .AsNoTracking()
            .Where(row => row.LanguageKey == normalizedLanguage)
            .Where(row =>
                (row.ContentType == CatalogContentType.Movie && movieIds.Contains(row.ContentId))
                || (row.ContentType == CatalogContentType.Tv && tvIds.Contains(row.ContentId)))
            .Select(row => new { row.ContentType, row.ContentId, row.PosterPath })
            .ToListAsync(cancellationToken);

        var keySet = keys.ToHashSet();
        var result = new Dictionary<ContentLocalizedPosterKey, string>();
        foreach (var row in rows)
        {
            var key = new ContentLocalizedPosterKey(row.ContentType, row.ContentId);
            if (keySet.Contains(key))
            {
                result[key] = row.PosterPath;
            }
        }

        return result;
    }

    public async Task UpsertAsync(
        CatalogContentType contentType,
        Guid contentId,
        string languageKey,
        string posterPath,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = languageKey.Trim().ToLowerInvariant();
        var existing = await dbContext.Set<ContentLocalizedPoster>()
            .FirstOrDefaultAsync(
                row => row.ContentType == contentType
                    && row.ContentId == contentId
                    && row.LanguageKey == normalizedLanguage,
                cancellationToken);

        if (existing is null)
        {
            dbContext.Set<ContentLocalizedPoster>().Add(new ContentLocalizedPoster
            {
                Id = Guid.NewGuid(),
                ContentType = contentType,
                ContentId = contentId,
                LanguageKey = normalizedLanguage,
                PosterPath = posterPath,
                UpdatedAtUtc = updatedAtUtc,
            });
        }
        else
        {
            existing.PosterPath = posterPath;
            existing.UpdatedAtUtc = updatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        CatalogContentType contentType,
        Guid contentId,
        string languageKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedLanguage = languageKey.Trim().ToLowerInvariant();
        var existing = await dbContext.Set<ContentLocalizedPoster>()
            .FirstOrDefaultAsync(
                row => row.ContentType == contentType
                    && row.ContentId == contentId
                    && row.LanguageKey == normalizedLanguage,
                cancellationToken);

        if (existing is null)
        {
            return;
        }

        dbContext.Set<ContentLocalizedPoster>().Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
