using System.Diagnostics;
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
    HotThisWeekLoadCoordinator loadCoordinator,
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

        var totalStopwatch = Stopwatch.StartNew();
        var cacheKey = HotThisWeekCacheKeys.Create(type, maxItems, contentLocale);

        var cacheLookupStopwatch = Stopwatch.StartNew();
        var cached = await cacheService.GetAsync<HotThisWeekCacheEntry>(cacheKey, cancellationToken);
        cacheLookupStopwatch.Stop();
        if (cached is not null)
        {
            LogCachePerf("CacheHit", cacheLookupStopwatch.ElapsedMilliseconds, 0, 0, 0, totalStopwatch, cached.Items.Count);
            return cached.Items;
        }

        var inFlight = loadCoordinator.TryGetInFlight(cacheKey);
        if (inFlight is not null)
        {
            var waitStopwatch = Stopwatch.StartNew();
            var shared = await inFlight;
            waitStopwatch.Stop();
            LogCachePerf(
                "WaiterInProcess",
                cacheLookupStopwatch.ElapsedMilliseconds,
                0,
                0,
                waitStopwatch.ElapsedMilliseconds,
                totalStopwatch,
                shared.Count);
            return shared;
        }

        var loaded = await loadCoordinator.RunInFlightAsync(
            cacheKey,
            () => LoadAndCacheAsync(
                type,
                maxItems,
                contentLocale,
                cacheKey,
                cancellationToken));

        totalStopwatch.Stop();
        return loaded;
    }

    private async Task<IReadOnlyList<SearchItem>> LoadAndCacheAsync(
        SearchContentType type,
        int maxItems,
        string contentLocale,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        long snapshotLookupMs = 0;
        long trendingFallbackMs = 0;
        string role;

        var snapshotStopwatch = Stopwatch.StartNew();
        var snapshot = await trendingSnapshotService.GetSnapshotAsync(cancellationToken);
        snapshotStopwatch.Stop();
        snapshotLookupMs = snapshotStopwatch.ElapsedMilliseconds;

        IReadOnlyList<SearchItem> items;
        DateTimeOffset? snapshotRefreshedAt = null;

        if (snapshot is { Items.Count: > 0 })
        {
            role = "SnapshotHit";
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
            role = "TrendingFallback";
            var trendingStopwatch = Stopwatch.StartNew();
            var discovery = await discoveryService.GetTrendingAsync(
                new DiscoveryCriteria(type, 1, maxItems),
                contentLocale,
                cancellationToken);
            trendingStopwatch.Stop();
            trendingFallbackMs = trendingStopwatch.ElapsedMilliseconds;
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

        totalStopwatch.Stop();
        LogCachePerf(role, 0, snapshotLookupMs, trendingFallbackMs, 0, totalStopwatch, items.Count);
        return items;
    }

    private void LogCachePerf(
        string role,
        long cacheLookupMs,
        long snapshotLookupMs,
        long trendingFallbackMs,
        long waitMs,
        Stopwatch totalStopwatch,
        int itemCount)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        HotThisWeekTrendingSnapshotLogMessages.LogCachePerf(
            logger,
            role,
            cacheLookupMs,
            snapshotLookupMs,
            trendingFallbackMs,
            waitMs,
            totalStopwatch.ElapsedMilliseconds,
            itemCount);
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
