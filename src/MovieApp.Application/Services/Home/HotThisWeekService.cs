using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.Home;

public sealed class HotThisWeekService(
    IDiscoveryService discoveryService,
    ICacheService cacheService,
    IOptions<HomeOptions> options) : IHotThisWeekService
{
    private readonly HomeOptions _options = options.Value;

    public async Task<IReadOnlyList<SearchItem>> GetItemsAsync(
        SearchContentType type,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            return [];
        }

        var cacheKey = HotThisWeekCacheKeys.Create(type, maxItems);
        var cached = await cacheService.GetAsync<HotThisWeekCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Items;
        }

        var discovery = await discoveryService.GetTrendingAsync(
            new DiscoveryCriteria(type, 1, maxItems),
            cancellationToken);

        var items = discovery.Items;

        await cacheService.SetAsync(
            cacheKey,
            new HotThisWeekCacheEntry { Items = items },
            TimeSpan.FromMinutes(_options.HotThisWeekCacheTtlMinutes),
            cancellationToken);

        return items;
    }
}

internal sealed class HotThisWeekCacheEntry
{
    public required IReadOnlyList<SearchItem> Items { get; init; }
}
