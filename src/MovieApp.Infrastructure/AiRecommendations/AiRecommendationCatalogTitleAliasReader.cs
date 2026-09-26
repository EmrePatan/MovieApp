using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.AiRecommendations;

internal sealed class AiRecommendationCatalogTitleAliasReader(ApplicationDbContext dbContext)
    : IAiRecommendationCatalogTitleAliasReader
{
    public async Task<IReadOnlyList<string>> GetTitleAliasesAsync(
        string mediaType,
        Guid contentId,
        CancellationToken cancellationToken = default)
    {
        var contentType = string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase)
            ? CatalogContentType.Tv
            : CatalogContentType.Movie;

        return await dbContext.ContentSearchTitles
            .AsNoTracking()
            .Where(row => row.ContentType == contentType && row.ContentId == contentId)
            .Select(row => row.Title)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
