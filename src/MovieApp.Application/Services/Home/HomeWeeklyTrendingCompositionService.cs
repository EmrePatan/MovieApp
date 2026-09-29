using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.Application.Services.Home;

public sealed class HomeWeeklyTrendingCompositionService(
    IHotThisWeekTrendingSnapshotService trendingSnapshotService,
    IDiscoveryService discoveryService,
    ISummaryLocalizationOverlayService summaryLocalizationOverlayService) : IHomeWeeklyTrendingCompositionService
{
    public async Task<HomeWeeklyTrendingCompositionResult> ComposeAsync(
        SearchContentType type,
        int heroSize,
        int trendingSize,
        string contentLocale,
        CancellationToken cancellationToken = default)
    {
        var normalizedHeroSize = Math.Max(0, heroSize);
        var normalizedTrendingSize = Math.Max(0, trendingSize);

        if (normalizedHeroSize == 0 && normalizedTrendingSize == 0)
        {
            return new HomeWeeklyTrendingCompositionResult([], [], HomeWeeklyTrendingReadSource.WeeklySnapshot);
        }

        var snapshot = await trendingSnapshotService.GetSnapshotAsync(cancellationToken);
        if (snapshot is { Items.Count: > 0 })
        {
            var filtered = HotThisWeekService.FilterAndTake(snapshot.Items, type, snapshot.Items.Count);
            var localized = await ApplyLocalizationAsync(filtered, contentLocale, cancellationToken);
            var (heroItems, trendingItems) = HomeWeeklyTrendingComposition.Split(
                localized,
                normalizedHeroSize,
                normalizedTrendingSize);

            return new HomeWeeklyTrendingCompositionResult(
                heroItems,
                trendingItems,
                HomeWeeklyTrendingReadSource.WeeklySnapshot);
        }

        var fallbackPageSize = normalizedHeroSize + normalizedTrendingSize;
        if (fallbackPageSize <= 0)
        {
            return new HomeWeeklyTrendingCompositionResult([], [], HomeWeeklyTrendingReadSource.CatalogFallback);
        }

        var discovery = await discoveryService.GetTrendingAsync(
            new DiscoveryCriteria(type, 1, fallbackPageSize),
            contentLocale,
            cancellationToken);
        var fallbackOrdered = discovery.Items.ToList();
        var (fallbackHero, fallbackTrending) = HomeWeeklyTrendingComposition.Split(
            fallbackOrdered,
            normalizedHeroSize,
            normalizedTrendingSize);

        return new HomeWeeklyTrendingCompositionResult(
            fallbackHero,
            fallbackTrending,
            HomeWeeklyTrendingReadSource.CatalogFallback);
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
