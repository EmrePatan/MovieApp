using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Home;

public sealed class HomeWeeklyTrendingSectionServiceTests
{
    [Fact]
    public async Task GetTrendingItemsAsyncUsesWeeklyOrderAndExcludesHeroItems()
    {
        var weekly = CreateWeeklyItems(12);
        var heroItems = weekly.Take(3).ToList();
        var discovery = new RecordingDiscoveryService([]);
        var service = CreateService(new FakeSnapshotService(weekly), discovery);

        var trending = await service.GetTrendingItemsAsync(
            SearchContentType.All,
            heroItems,
            heroSize: 3,
            trendingSize: 4,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(
            ["Weekly 4", "Weekly 5", "Weekly 6", "Weekly 7"],
            trending.Select(item => item.Title).ToList());
        Assert.Equal(0, discovery.TrendingCallCount);
    }

    [Fact]
    public async Task GetTrendingItemsAsyncExcludesHeroByContentIdentity()
    {
        var sharedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        List<SearchItem> weekly =
        [
            CreateSearchItem("movie", sharedId, "Weekly Movie"),
            CreateSearchItem("tv", sharedId, "Weekly Tv Same Id"),
            CreateSearchItem("movie", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Weekly Movie Two"),
        ];

        var heroItems = weekly.Take(1).ToList();
        var service = CreateService(new FakeSnapshotService(weekly), new RecordingDiscoveryService([]));

        var trending = await service.GetTrendingItemsAsync(
            SearchContentType.All,
            heroItems,
            heroSize: 1,
            trendingSize: 5,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(2, trending.Count);
        Assert.DoesNotContain(trending, item => item.Type == "movie" && item.Id == sharedId);
        Assert.Contains(trending, item => item.Type == "tv" && item.Id == sharedId);
    }

    [Fact]
    public async Task GetTrendingItemsAsyncFiltersMovieTypeBeforeExcludingHero()
    {
        List<SearchItem> weekly =
        [
            CreateSearchItem("tv", Guid.Parse("11111111-1111-1111-1111-111111111101"), "Tv One"),
            CreateSearchItem("movie", Guid.Parse("22222222-2222-2222-2222-222222222201"), "Movie One"),
            CreateSearchItem("movie", Guid.Parse("33333333-3333-3333-3333-333333333301"), "Movie Two"),
        ];

        var heroItems = new List<SearchItem> { weekly[1] };
        var service = CreateService(new FakeSnapshotService(weekly), new RecordingDiscoveryService([]));

        var trending = await service.GetTrendingItemsAsync(
            SearchContentType.Movie,
            heroItems,
            heroSize: 1,
            trendingSize: 2,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["Movie Two"], trending.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task GetTrendingItemsAsyncUsesCatalogFallbackWhenSnapshotMissing()
    {
        var fallbackItems = CreateWeeklyItems(8);
        var heroItems = fallbackItems.Take(3).ToList();
        var discovery = new RecordingDiscoveryService(fallbackItems);
        var service = CreateService(new FakeSnapshotService(null), discovery);

        var trending = await service.GetTrendingItemsAsync(
            SearchContentType.All,
            heroItems,
            heroSize: 3,
            trendingSize: 3,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, discovery.TrendingCallCount);
        Assert.Equal(6, discovery.LastTrendingPageSize);
        Assert.Equal(
            ["Weekly 4", "Weekly 5", "Weekly 6"],
            trending.Select(item => item.Title).ToList());
    }

    [Fact]
    public void SelectTrendingItemsPreservesWeeklyOrderAfterHeroRemoval()
    {
        var weekly = CreateWeeklyItems(6);
        var heroItems = weekly.Take(2).ToList();

        var trending = HomeWeeklyTrendingComposition.SelectTrendingItems(weekly, heroItems, 3);

        Assert.Equal(["Weekly 3", "Weekly 4", "Weekly 5"], trending.Select(item => item.Title).ToList());
    }

    [Fact]
    public void SelectTrendingItemsDoesNotDuplicateHeroItems()
    {
        var weekly = CreateWeeklyItems(5);
        var heroItems = weekly.Take(2).ToList();

        var trending = HomeWeeklyTrendingComposition.SelectTrendingItems(weekly, heroItems, 5);

        Assert.DoesNotContain(
            trending,
            item => heroItems.Any(heroItem => heroItem.Type == item.Type && heroItem.Id == item.Id));
    }

    [Fact]
    public async Task HotThisWeekServiceMatchesPreChangeHeroHeadSelection()
    {
        var weekly = CreateWeeklyItems(10);
        var hotThisWeek = new HotThisWeekService(
            new RecordingDiscoveryService([]),
            new FakeSnapshotService(weekly),
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            new NoOpCacheService(),
            new HotThisWeekLoadCoordinator(),
            Options.Create(new HomeOptions { HotThisWeekCacheTtlMinutes = 30 }),
            NullLogger<HotThisWeekService>.Instance);

        var heroItems = await hotThisWeek.GetItemsAsync(SearchContentType.All, 3, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(
            ["Weekly 1", "Weekly 2", "Weekly 3"],
            heroItems.Select(item => item.Title).ToList());
    }

    private static HomeWeeklyTrendingSectionService CreateService(
        FakeSnapshotService snapshotService,
        RecordingDiscoveryService discoveryService) =>
        new(
            snapshotService,
            discoveryService,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService());

    private static List<SearchItem> CreateWeeklyItems(int count) =>
        Enumerable.Range(1, count)
            .Select(index =>
            {
                var type = index % 2 == 0 ? "tv" : "movie";
                return CreateSearchItem(
                    type,
                    Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{index:D12}"),
                    $"Weekly {index}");
            })
            .ToList();

    private static SearchItem CreateSearchItem(string type, Guid id, string title) =>
        new(
            id,
            type,
            title,
            null,
            null,
            "/poster.jpg",
            "/backdrop.jpg",
            null,
            6.1m,
            3,
            2025);

    private sealed class FakeSnapshotService(IReadOnlyList<SearchItem>? items) : IHotThisWeekTrendingSnapshotService
    {
        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            if (items is null || items.Count == 0)
            {
                return Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(null);
            }

            return Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(new HotThisWeekTrendingSnapshotEntry
            {
                RefreshedAt = DateTimeOffset.UtcNow,
                Items = items,
            });
        }

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingDiscoveryService(IReadOnlyList<SearchItem> trendingItems) : IDiscoveryService
    {
        public int TrendingCallCount { get; private set; }

        public int LastTrendingPageSize { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;
            LastTrendingPageSize = criteria.PageSize;

            var items = trendingItems
                .Where(item => criteria.Type switch
                {
                    SearchContentType.Movie => item.Type == "movie",
                    SearchContentType.Tv => item.Type == "tv",
                    _ => item.Type is "movie" or "tv",
                })
                .Take(criteria.PageSize)
                .ToList();

            return Task.FromResult(new PaginatedResult<SearchItem>(
                items,
                criteria.Page,
                criteria.PageSize,
                items.Count,
                items.Count == 0 ? 0 : 1));
        }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
