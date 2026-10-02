using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.Home;

public sealed class HomeWeeklyTrendingSectionService(
    IHotThisWeekTrendingSnapshotService trendingSnapshotService,
    ITrendingWeekListService trendingWeekListService,
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService) : IHomeWeeklyTrendingSectionService
{
    public async Task<IReadOnlyList<SearchItem>> GetTrendingItemsAsync(
        SearchContentType type,
        IReadOnlyList<SearchItem> heroItems,
        int heroSize,
        int trendingSize,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var normalizedTrendingSize = Math.Max(0, trendingSize);
        if (normalizedTrendingSize == 0)
        {
            return [];
        }

        var snapshot = await trendingSnapshotService.GetSnapshotAsync(cancellationToken);
        if (snapshot is { Items.Count: > 0 })
        {
            var filtered = HotThisWeekService.FilterAndTake(snapshot.Items, type, snapshot.Items.Count);
            var localized = await ApplyLocalizationAsync(filtered, contentLocale, cancellationToken);
            return HomeWeeklyTrendingComposition.SelectTrendingItems(
                localized,
                heroItems,
                normalizedTrendingSize);
        }

        var fallbackPageSize = Math.Max(heroItems.Count, Math.Max(0, heroSize)) + normalizedTrendingSize;
        if (fallbackPageSize <= 0)
        {
            return [];
        }

        var discovery = await trendingWeekListService.GetPageAsync(
            new DiscoveryCriteria(type, 1, fallbackPageSize),
            contentLocale,
            cancellationToken);

        return HomeWeeklyTrendingComposition.SelectTrendingItems(
            discovery.Items.ToList(),
            heroItems,
            normalizedTrendingSize);
    }

    private async Task<List<SearchItem>> ApplyLocalizationAsync(
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
        return localized.Items.ToList();
    }
}
