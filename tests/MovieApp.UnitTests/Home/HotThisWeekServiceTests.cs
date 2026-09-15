using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Search;
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
    public async Task GetItemsAsyncCachesCatalogTrendingResults()
    {
        var cache = new TrackingCacheService();
        var discovery = new RecordingDiscoveryService(CreateTrendingItems());
        var service = CreateService(cache, discovery);

        var first = await service.GetItemsAsync(SearchContentType.All, 5);
        var second = await service.GetItemsAsync(SearchContentType.All, 5);

        Assert.Equal(3, first.Count);
        Assert.Equal(first.Select(item => item.Id), second.Select(item => item.Id));
        Assert.Equal(2, cache.GetCount);
        Assert.Equal(1, cache.SetCount);
        Assert.Equal(1, discovery.TrendingCallCount);
    }

    [Fact]
    public async Task GetItemsAsyncDoesNotInvokeTmdbTrendingProvider()
    {
        var discovery = new RecordingDiscoveryService(CreateTrendingItems());
        var service = CreateService(new TrackingCacheService(), discovery);

        var items = await service.GetItemsAsync(SearchContentType.All, 5);

        Assert.Equal(3, items.Count);
        Assert.Equal(1, discovery.TrendingCallCount);
    }

    [Fact]
    public async Task GetItemsAsyncPopulatesHeroFromCatalogTrending()
    {
        var discovery = new RecordingDiscoveryService(CreateTrendingItems());
        var service = CreateService(new TrackingCacheService(), discovery);

        var items = await service.GetItemsAsync(SearchContentType.All, 2);

        Assert.Equal(
            ["Trending Movie One", "Trending Show One"],
            items.Select(item => item.Title).ToList());
        Assert.Equal(["movie", "tv"], items.Select(item => item.Type).ToList());
    }

    [Fact]
    public async Task GetItemsAsyncReturnsEmptyWhenCatalogTrendingIsEmpty()
    {
        var discovery = new RecordingDiscoveryService([]);
        var service = CreateService(new TrackingCacheService(), discovery);

        var items = await service.GetItemsAsync(SearchContentType.All, 5);

        Assert.Empty(items);
        Assert.Equal(1, discovery.TrendingCallCount);
    }

    private static HotThisWeekService CreateService(
        ICacheService cache,
        IDiscoveryService discovery) =>
        new(
            discovery,
            cache,
            Options.Create(new HomeOptions { HotThisWeekCacheTtlMinutes = 30 }));

    private static IReadOnlyList<SearchItem> CreateTrendingItems() =>
    [
        CreateSearchItem("movie", Guid.Parse("11111111-1111-1111-1111-111111111101"), "Trending Movie One"),
        CreateSearchItem("tv", Guid.Parse("22222222-2222-2222-2222-222222222201"), "Trending Show One"),
        CreateSearchItem("movie", Guid.Parse("11111111-1111-1111-1111-111111111102"), "Trending Movie Two"),
    ];

    private static SearchItem CreateSearchItem(string type, Guid id, string title) =>
        new(id, type, title, null, null, "/poster.jpg", null, null, 8m, 100, null);

    private sealed class RecordingDiscoveryService(IReadOnlyList<SearchItem> trendingItems) : IDiscoveryService
    {
        public int TrendingCallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
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

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static bool MatchesType(SearchItem item, SearchContentType type) =>
            type switch
            {
                SearchContentType.Movie => item.Type == "movie",
                SearchContentType.Tv => item.Type == "tv",
                _ => item.Type is "movie" or "tv"
            };
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
