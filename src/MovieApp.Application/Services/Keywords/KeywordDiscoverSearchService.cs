using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;

namespace MovieApp.Application.Services.Keywords;

public sealed class KeywordDiscoverSearchService(
    IKeywordDiscoverReadRepository keywordDiscoverReadRepository,
    ICacheService cacheService,
    KeywordDiscoverSearchLoadCoordinator loadCoordinator) : IKeywordDiscoverSearchService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public async Task<PaginatedResult<KeywordDiscoverItem>> SearchAsync(
        string query,
        string contentLocale,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = KeywordDiscoverCacheKeys.Create(query, contentLocale, page, pageSize);
        var cached = await cacheService.GetAsync<KeywordDiscoverCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Result;
        }

        return await loadCoordinator.RunAsync(
            cacheKey,
            () => SearchAndCacheAsync(query, contentLocale, page, pageSize, cacheKey, CancellationToken.None));
    }

    private async Task<PaginatedResult<KeywordDiscoverItem>> SearchAndCacheAsync(
        string query,
        string contentLocale,
        int page,
        int pageSize,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var raced = await cacheService.GetAsync<KeywordDiscoverCacheEntry>(cacheKey, cancellationToken);
        if (raced is not null)
        {
            return raced.Result;
        }

        var result = await keywordDiscoverReadRepository.SearchAsync(
            query,
            contentLocale,
            page,
            pageSize,
            cancellationToken);

        await cacheService.SetAsync(
            cacheKey,
            new KeywordDiscoverCacheEntry { Result = result },
            CacheTtl,
            cancellationToken);

        return result;
    }
}
