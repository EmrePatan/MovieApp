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

public sealed class HotThisWeekConcurrentLoadTests
{
    [Fact]
    public async Task ConcurrentMissesExecuteSnapshotLoadOnce()
    {
        var coordinator = new HotThisWeekLoadCoordinator();
        var cache = new TrackingCacheService();
        var snapshot = new CountingSnapshotService(CreateItems());
        var discovery = new NoOpDiscoveryService();
        var service = new HotThisWeekService(
            discovery,
            snapshot,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            cache,
            coordinator,
            Options.Create(new HomeOptions { HotThisWeekCacheTtlMinutes = 30 }),
            NullLogger<HotThisWeekService>.Instance);

        var tasks = Enumerable.Range(0, 4)
            .Select(_ => service.GetItemsAsync(SearchContentType.All, 5, ContentLocaleResolver.EnglishUnitedStates))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, snapshot.GetSnapshotCallCount);
        Assert.Equal(0, discovery.TrendingCallCount);
        Assert.Equal(1, cache.SetCount);
    }

    private static IReadOnlyList<SearchItem> CreateItems() =>
        [new SearchItem(Guid.NewGuid(), "movie", "Weekly", null, null, null, null, null, 8m, 10, null)];

    private sealed class CountingSnapshotService(IReadOnlyList<SearchItem> items) : IHotThisWeekTrendingSnapshotService
    {
        private int _getSnapshotCallCount;

        public int GetSnapshotCallCount => _getSnapshotCallCount;

        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _getSnapshotCallCount);
            return Task.FromResult<HotThisWeekTrendingSnapshotEntry?>(new HotThisWeekTrendingSnapshotEntry
            {
                RefreshedAt = DateTimeOffset.UtcNow,
                Items = items
            });
        }

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpDiscoveryService : IDiscoveryService
    {
        public int TrendingCallCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;
            return Task.FromResult(new PaginatedResult<SearchItem>([], 1, 10, 0, 0));
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

    private sealed class TrackingCacheService : ICacheService
    {
        public int SetCount { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class
        {
            SetCount++;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
