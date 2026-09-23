using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.Home;

public sealed class HotThisWeekService(
    IDiscoveryService discoveryService,
    IHotThisWeekTrendingSnapshotService trendingSnapshotService,
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService,
    ICacheService cacheService,
    IOptions<HomeOptions> options,
    ILogger<HotThisWeekService> logger) : IHotThisWeekService
{
    private readonly HomeOptions _options = options.Value;

    public async Task<IReadOnlyList<SearchItem>> GetItemsAsync(
        SearchContentType type,
        int maxItems,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        if (maxItems <= 0)
        {
            return [];
        }

        var cacheKey = HotThisWeekCacheKeys.Create(type, maxItems, contentLocale);
        var cached = await cacheService.GetAsync<HotThisWeekCacheEntry>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached.Items;
        }

        var snapshot = await trendingSnapshotService.GetSnapshotAsync(cancellationToken);
        IReadOnlyList<SearchItem> items;
        DateTimeOffset? snapshotRefreshedAt = null;

        if (snapshot is { Items.Count: > 0 })
        {
            var filtered = FilterAndTake(snapshot.Items, type, maxItems);
            items = await ApplySnapshotLocalizationAsync(filtered, contentLocale, cancellationToken);
            snapshotRefreshedAt = snapshot.RefreshedAt;
            HotThisWeekTrendingSnapshotLogMessages.LogReadSource(
                logger,
                HotThisWeekReadSources.WeeklySnapshot,
                items.Count,
                snapshotRefreshedAt);
        }
        else
        {
            var discovery = await discoveryService.GetTrendingAsync(
                new DiscoveryCriteria(type, 1, maxItems),
                contentLocale,
                cancellationToken);
            items = discovery.Items;
            HotThisWeekTrendingSnapshotLogMessages.LogReadSource(
                logger,
                HotThisWeekReadSources.CatalogFallback,
                items.Count,
                null);
        }

        await cacheService.SetAsync(
            cacheKey,
            new HotThisWeekCacheEntry { Items = items },
            TimeSpan.FromMinutes(_options.HotThisWeekCacheTtlMinutes),
            cancellationToken);

        return items;
    }

    internal static List<SearchItem> FilterAndTake(
        IReadOnlyList<SearchItem> items,
        SearchContentType type,
        int maxItems)
    {
        IEnumerable<SearchItem> filtered = type switch
        {
            SearchContentType.Movie => items.Where(item => item.Type == "movie"),
            SearchContentType.Tv => items.Where(item => item.Type == "tv"),
            _ => items.Where(item => item.Type is "movie" or "tv"),
        };

        return filtered.Take(maxItems).ToList();
    }

    private async Task<IReadOnlyList<SearchItem>> ApplySnapshotLocalizationAsync(
        List<SearchItem> items,
        string contentLocale,
        CancellationToken cancellationToken)
    {
        if (!ContentLocaleResolver.RequiresLocalization(contentLocale) || items.Count == 0)
        {
            return items;
        }

        var page = new PaginatedResult<SearchItem>(
            items,
            1,
            items.Count,
            items.Count,
            1);
        var localized = await summaryLocalizationOverlayService.ApplyToSearchItemsAsync(
            page,
            contentLocale,
            cancellationToken);
        return localized.Items;
    }
}

internal sealed class HotThisWeekCacheEntry
{
    public required IReadOnlyList<SearchItem> Items { get; init; }
}
