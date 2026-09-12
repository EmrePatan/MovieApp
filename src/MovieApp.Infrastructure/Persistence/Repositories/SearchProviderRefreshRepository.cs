using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Search;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class SearchProviderRefreshRepository(ApplicationDbContext dbContext) : ISearchProviderRefreshRepository
{
    public async Task<DateTime?> GetLastRefreshedAtUtcAsync(
        string normalizedQuery,
        SearchContentType contentType,
        int page,
        CancellationToken cancellationToken = default)
    {
        var mappedContentType = MapContentType(contentType);

        var refresh = await dbContext.SearchProviderRefreshes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.NormalizedQuery == normalizedQuery &&
                    item.ContentType == mappedContentType &&
                    item.Page == page,
                cancellationToken);

        return refresh?.LastRefreshedAtUtc;
    }

    public async Task SetLastRefreshedAtUtcAsync(
        string normalizedQuery,
        SearchContentType contentType,
        int page,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var mappedContentType = MapContentType(contentType);

        var refresh = await dbContext.SearchProviderRefreshes
            .FirstOrDefaultAsync(
                item =>
                    item.NormalizedQuery == normalizedQuery &&
                    item.ContentType == mappedContentType &&
                    item.Page == page,
                cancellationToken);

        if (refresh is null)
        {
            refresh = new SearchProviderRefresh
            {
                Id = Guid.NewGuid(),
                NormalizedQuery = normalizedQuery,
                ContentType = mappedContentType,
                Page = page
            };

            dbContext.SearchProviderRefreshes.Add(refresh);
        }

        refresh.LastRefreshedAtUtc = refreshedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static SearchProviderContentType MapContentType(SearchContentType contentType) =>
        contentType switch
        {
            SearchContentType.Movie => SearchProviderContentType.Movie,
            SearchContentType.Tv => SearchProviderContentType.Tv,
            _ => SearchProviderContentType.All
        };
}
