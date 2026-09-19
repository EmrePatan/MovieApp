using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Search;
using MovieApp.Infrastructure.Caching;

namespace MovieApp.UnitTests.Home;

public sealed class HomeGlobalSectionsProviderTests
{
    [Fact]
    public async Task GetOrLoadAsyncCachesGlobalSectionsAcrossCalls()
    {
        var cache = new TrackingCacheService();
        var discovery = new CountingDiscoveryService();
        var provider = CreateProvider(cache, discovery);

        var criteria = new HomeCriteria(SearchContentType.All, 5);
        var first = await provider.GetOrLoadAsync(criteria);
        var second = await provider.GetOrLoadAsync(criteria);

        Assert.Equal(first.Trending.Items[0].Id, second.Trending.Items[0].Id);
        Assert.Equal(1, discovery.TrendingCallCount);
        Assert.Equal(1, discovery.PopularCallCount);
        Assert.Equal(1, discovery.NewReleasesCallCount);
        Assert.Equal(1, discovery.TopRatedCallCount);
        Assert.Equal(1, discovery.GenreCallCount);
        Assert.Equal(1, cache.GlobalSetCount);
        Assert.True(cache.GlobalGetCount >= 2);
    }

    [Fact]
    public async Task GetOrLoadAsyncReusesCachedBlockForDifferentUsers()
    {
        var cache = new TrackingCacheService();
        var discovery = new CountingDiscoveryService();
        var provider = CreateProvider(cache, discovery);
        var criteria = new HomeCriteria(SearchContentType.All, 5);

        await provider.GetOrLoadAsync(criteria);
        await provider.GetOrLoadAsync(criteria);

        Assert.Equal(1, discovery.TrendingCallCount);
        Assert.DoesNotContain(cache.StoredKeys, key => key.Contains("user", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetOrLoadAsyncIsolatesCacheByMediaType()
    {
        var cache = new TrackingCacheService();
        var discovery = new CountingDiscoveryService();
        var provider = CreateProvider(cache, discovery);

        await provider.GetOrLoadAsync(new HomeCriteria(SearchContentType.All, 5));
        await provider.GetOrLoadAsync(new HomeCriteria(SearchContentType.Movie, 5));
        await provider.GetOrLoadAsync(new HomeCriteria(SearchContentType.Tv, 5));

        Assert.Equal(3, discovery.TrendingCallCount);
        Assert.Equal(3, cache.GlobalSetCount);
    }

    [Fact]
    public async Task GetOrLoadAsyncIsolatesCacheBySectionSize()
    {
        var cache = new TrackingCacheService();
        var discovery = new CountingDiscoveryService();
        var provider = CreateProvider(cache, discovery);

        await provider.GetOrLoadAsync(new HomeCriteria(SearchContentType.All, 5));
        await provider.GetOrLoadAsync(new HomeCriteria(SearchContentType.All, 10));

        Assert.Equal(2, discovery.TrendingCallCount);
        Assert.Equal(2, cache.GlobalSetCount);
    }

    private static HomeGlobalSectionsProvider CreateProvider(
        ICacheService cache,
        CountingDiscoveryService discovery) =>
        new(
            discovery,
            cache,
            new InMemorySearchRefreshLockService(),
            Options.Create(new HomeOptions
            {
                GenreSections = ["Science Fiction"]
            }));

    private sealed class CountingDiscoveryService : IDiscoveryService
    {
        public int TrendingCallCount { get; private set; }

        public int PopularCallCount { get; private set; }

        public int NewReleasesCallCount { get; private set; }

        public int TopRatedCallCount { get; private set; }

        public int GenreCallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            PopularCallCount++;
            return Task.FromResult(CreateResult(criteria, "movie", 10));
        }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;
            return Task.FromResult(CreateResult(criteria, "tv", 20));
        }

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            NewReleasesCallCount++;
            return Task.FromResult(CreateResult(criteria, "movie", 30));
        }

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            TopRatedCallCount++;
            return Task.FromResult(CreateResult(criteria, "tv", 40));
        }

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, string contentLocale, CancellationToken cancellationToken = default)
        {
            GenreCallCount++;
            return Task.FromResult(CreateResult(criteria, "movie", 50));
        }

        private static PaginatedResult<SearchItem> CreateResult(
            DiscoveryCriteria criteria,
            string type,
            int seed)
        {
            var item = new SearchItem(
                Guid.Parse($"eeeeeeee-eeee-eeee-eeee-{seed:D012}"),
                type,
                $"Discovery {seed}",
                null,
                null,
                null,
                null,
                new DateOnly(2021, 1, 1),
                7m,
                50,
                2021);

            return new PaginatedResult<SearchItem>([item], 1, criteria.PageSize, 1, 1);
        }
    }

    private sealed class TrackingCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public int GlobalGetCount { get; private set; }

        public int GlobalSetCount { get; private set; }

        public IReadOnlyCollection<string> StoredKeys => _entries.Keys;

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (key.StartsWith(HomeGlobalCacheKeys.Prefix, StringComparison.Ordinal))
            {
                GlobalGetCount++;
            }

            return Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            if (key.StartsWith(HomeGlobalCacheKeys.Prefix, StringComparison.Ordinal))
            {
                GlobalSetCount++;
            }

            _entries[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySearchRefreshLockService : ISearchRefreshLockService
    {
        private readonly LocalSearchRefreshSingleFlightGate _localGate = new();

        public Task<SearchRefreshLockHandle?> TryAcquireAsync(
            string lockKey,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default)
        {
            if (_localGate.TryAcquire(lockKey, out var lockToken))
            {
                return Task.FromResult<SearchRefreshLockHandle?>(
                    new SearchRefreshLockHandle(lockKey, lockToken, SearchRefreshLockBackend.LocalSingleFlight));
            }

            return Task.FromResult<SearchRefreshLockHandle?>(null);
        }

        public Task ReleaseAsync(
            string lockKey,
            string lockToken,
            SearchRefreshLockBackend backend,
            CancellationToken cancellationToken = default)
        {
            _localGate.Release(lockKey, lockToken);
            return Task.CompletedTask;
        }

        public Task<bool> TryRenewAsync(
            string lockKey,
            string lockToken,
            SearchRefreshLockBackend backend,
            TimeSpan lockDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
