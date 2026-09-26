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
using MovieApp.Infrastructure.Providers;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbMapping;
using MovieApp.Infrastructure.Providers.Tmdb.TmdbModels;

namespace MovieApp.UnitTests.Home;

public sealed class HotThisWeekServiceTests
{
    [Fact]
    public void TmdbTrendingMapperExcludesPersonResults()
    {
        var person = TmdbTrendingMapper.ToProviderItem(new TmdbTrendingResultJson
        {
            Id = FakeTrendingWeekDataProvider.TrendingPersonTmdbId,
            MediaType = "person",
            Name = "Famous Person"
        });

        Assert.Null(person);
    }

    [Fact]
    public async Task GetItemsAsyncReadsWeeklySnapshotWithoutCallingDiscovery()
    {
        var cache = new TrackingCacheService();
        var discovery = new RecordingDiscoveryService(CreateTrendingItems());
        var snapshot = new RecordingTrendingSnapshotService(CreateSnapshotItems());
        var service = CreateService(cache, discovery, snapshot);

        var items = await service.GetItemsAsync(SearchContentType.All, 5, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(
            ["Weekly Movie One", "Weekly Show One", "Weekly Movie Two"],
            items.Select(item => item.Title).ToList());
        Assert.Equal(0, discovery.TrendingCallCount);
        Assert.Equal(1, snapshot.GetSnapshotCallCount);
    }

    [Fact]
    public async Task GetItemsAsyncUsesCatalogFallbackWhenSnapshotMissing()
    {
        var discovery = new RecordingDiscoveryService(CreateTrendingItems());
        var service = CreateService(new TrackingCacheService(), discovery, new RecordingTrendingSnapshotService(null));

        var items = await service.GetItemsAsync(SearchContentType.All, 5, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(3, items.Count);
        Assert.Equal(1, discovery.TrendingCallCount);
        Assert.Equal(["Trending Movie One", "Trending Show One", "Trending Movie Two"], items.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task GetItemsAsyncFiltersSnapshotByMovieType()
    {
        var service = CreateService(
            new TrackingCacheService(),
            new RecordingDiscoveryService([]),
            new RecordingTrendingSnapshotService(CreateSnapshotItems()));

        var items = await service.GetItemsAsync(SearchContentType.Movie, 5, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(
            ["Weekly Movie One", "Weekly Movie Two"],
            items.Select(item => item.Title).ToList());
        Assert.All(items, item => Assert.Equal("movie", item.Type));
    }

    [Fact]
    public async Task GetItemsAsyncFiltersSnapshotByTvType()
    {
        var service = CreateService(
            new TrackingCacheService(),
            new RecordingDiscoveryService([]),
            new RecordingTrendingSnapshotService(CreateSnapshotItems()));

        var items = await service.GetItemsAsync(SearchContentType.Tv, 5, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(items);
        Assert.Equal("Weekly Show One", items[0].Title);
        Assert.Equal("tv", items[0].Type);
    }

    [Fact]
    public async Task GetItemsAsyncRespectsMaxItemsFromWeeklySnapshot()
    {
        var service = CreateService(
            new TrackingCacheService(),
            new RecordingDiscoveryService([]),
            new RecordingTrendingSnapshotService(CreateSnapshotItems()));

        var items = await service.GetItemsAsync(SearchContentType.All, 2, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["Weekly Movie One", "Weekly Show One"], items.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task GetItemsAsyncCachesSliceResults()
    {
        var cache = new TrackingCacheService();
        var discovery = new RecordingDiscoveryService(CreateTrendingItems());
        var snapshot = new RecordingTrendingSnapshotService(CreateSnapshotItems());
        var service = CreateService(cache, discovery, snapshot);

        var first = await service.GetItemsAsync(SearchContentType.All, 5, ContentLocaleResolver.EnglishUnitedStates);
        var second = await service.GetItemsAsync(SearchContentType.All, 5, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(first.Select(item => item.Id), second.Select(item => item.Id));
        Assert.Equal(2, cache.GetCount);
        Assert.Equal(1, cache.SetCount);
        Assert.Equal(1, snapshot.GetSnapshotCallCount);
    }

    [Fact]
    public void FilterAndTakePreservesSnapshotOrderForAllType()
    {
        var items = HotThisWeekService.FilterAndTake(CreateSnapshotItems(), SearchContentType.All, 10);

        Assert.Equal(
            ["Weekly Movie One", "Weekly Show One", "Weekly Movie Two"],
            items.Select(item => item.Title).ToList());
    }

    private static HotThisWeekService CreateService(
        ICacheService cache,
        IDiscoveryService discovery,
        IHotThisWeekTrendingSnapshotService snapshotService) =>
        new(
            discovery,
            snapshotService,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            cache,
            new HotThisWeekLoadCoordinator(),
            Options.Create(new HomeOptions { HotThisWeekCacheTtlMinutes = 30 }),
            NullLogger<HotThisWeekService>.Instance);

    private static IReadOnlyList<SearchItem> CreateTrendingItems() =>
    [
        CreateSearchItem("movie", Guid.Parse("11111111-1111-1111-1111-111111111101"), "Trending Movie One"),
        CreateSearchItem("tv", Guid.Parse("22222222-2222-2222-2222-222222222201"), "Trending Show One"),
        CreateSearchItem("movie", Guid.Parse("11111111-1111-1111-1111-111111111102"), "Trending Movie Two"),
    ];

    private static IReadOnlyList<SearchItem> CreateSnapshotItems() =>
    [
        CreateSearchItem("movie", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Weekly Movie One"),
        CreateSearchItem("tv", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Weekly Show One"),
        CreateSearchItem("movie", Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Weekly Movie Two"),
    ];

    private static SearchItem CreateSearchItem(string type, Guid id, string title) =>
        new(id, type, title, null, null, "/poster.jpg", null, null, 8m, 100, null);

    private sealed class RecordingDiscoveryService(IReadOnlyList<SearchItem> trendingItems) : IDiscoveryService
    {
        public int TrendingCallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;

            var items = trendingItems
                .Where(item => MatchesType(item, criteria.Type))
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

        private static bool MatchesType(SearchItem item, SearchContentType type) =>
            type switch
            {
                SearchContentType.Movie => item.Type == "movie",
                SearchContentType.Tv => item.Type == "tv",
                _ => item.Type is "movie" or "tv"
            };
    }

    private sealed class RecordingTrendingSnapshotService(IReadOnlyList<SearchItem>? items)
        : IHotThisWeekTrendingSnapshotService
    {
        public int GetSnapshotCallCount { get; private set; }

        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            GetSnapshotCallCount++;

            if (items is null || items.Count == 0)
            {
                return Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(null);
            }

            return Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(new HotThisWeekTrendingSnapshotEntry
            {
                RefreshedAt = DateTimeOffset.UtcNow.AddHours(-1),
                Items = items,
            });
        }

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TrackingCacheService : ICacheService
    {
        public int GetCount { get; private set; }

        public int SetCount { get; private set; }

        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            GetCount++;
            return Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default) where T : class
        {
            SetCount++;
            _entries[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }
}
