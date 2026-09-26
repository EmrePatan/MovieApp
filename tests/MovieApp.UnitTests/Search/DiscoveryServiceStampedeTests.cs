using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoveryServiceStampedeTests
{
    [Fact]
    public async Task ConcurrentMissesForSameKeyExecuteCanonicalLoadOnce()
    {
        var cache = new SharedDiscoveryCacheService();
        var repository = new SlowTrendingRepository(TimeSpan.FromMilliseconds(400));
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var service = CreateService(repository, cache, lockService);

        var criteria = new DiscoveryCriteria(SearchContentType.All, 1, 10);
        var tasks = Enumerable.Range(0, 12)
            .Select(_ => service.GetTrendingAsync(criteria, ContentLocaleResolver.EnglishUnitedStates))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(12, results.Length);
        Assert.All(results, result => Assert.Single(result.Items));
        Assert.Equal(1, repository.TrendingLoadCount);
        Assert.Equal(1, cache.SetCountForPrefix(DiscoveryTrendingCacheKeys.Prefix));
    }

    [Fact]
    public async Task ConcurrentMissesForDifferentKeysDoNotBlockEachOther()
    {
        var cache = new SharedDiscoveryCacheService();
        var repository = new DualSlowRepository(TimeSpan.FromMilliseconds(250));
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var service = CreateService(repository, cache, lockService);

        var trendingCriteria = new DiscoveryCriteria(SearchContentType.All, 1, 10);
        var popularCriteria = new DiscoveryCriteria(SearchContentType.Movie, 1, 10);

        await Task.WhenAll(
            service.GetTrendingAsync(trendingCriteria, ContentLocaleResolver.EnglishUnitedStates),
            service.GetPopularAsync(popularCriteria, ContentLocaleResolver.EnglishUnitedStates),
            service.GetTrendingAsync(trendingCriteria, ContentLocaleResolver.EnglishUnitedStates),
            service.GetPopularAsync(popularCriteria, ContentLocaleResolver.EnglishUnitedStates));

        Assert.Equal(1, repository.TrendingLoadCount);
        Assert.Equal(1, repository.PopularLoadCount);
    }

    [Fact]
    public async Task CacheHitDoesNotAcquireRefreshLock()
    {
        var items = CreateItems(1);
        var cached = Paginated(items);
        var cacheKey = DiscoveryTrendingCacheKeys.Create(
            new DiscoveryCriteria(SearchContentType.All, 1, 10),
            ContentLocaleResolver.EnglishUnitedStates);
        var cache = new SharedDiscoveryCacheService();
        await cache.SetAsync(
            cacheKey,
            new DiscoveryCacheEntry { Result = cached },
            TimeSpan.FromMinutes(5));

        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var repository = new SlowTrendingRepository(TimeSpan.Zero);
        var service = CreateService(repository, cache, lockService);

        var result = await service.GetTrendingAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, 10),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(result.Items);
        Assert.Equal(0, repository.TrendingLoadCount);
        Assert.Equal(0, lockService.AcquireAttempts);
    }

    [Fact]
    public async Task WhenLockAcquisitionAlwaysDeniedStillReturnsResults()
    {
        var cache = new SharedDiscoveryCacheService();
        var repository = new SlowTrendingRepository(TimeSpan.Zero);
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService { ForceDenyAcquire = true };
        var service = CreateService(repository, cache, lockService);

        var criteria = new DiscoveryCriteria(SearchContentType.All, 1, 10);
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => service.GetTrendingAsync(criteria, ContentLocaleResolver.EnglishUnitedStates))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(5, results.Length);
        Assert.All(results, result => Assert.Single(result.Items));
        Assert.True(repository.TrendingLoadCount >= 1);
        Assert.Equal(5, lockService.AcquireAttempts);
    }

    private static DiscoveryService CreateService(
        ISearchRepository repository,
        ICacheService cache,
        ISearchRefreshLockService lockService) =>
        new(
            repository,
            cache,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            lockService,
            new DiscoveryCacheLoadCoordinator(),
            NullLogger<DiscoveryService>.Instance);

    private static List<SearchItem> CreateItems(int count)
    {
        var items = new List<SearchItem>();
        for (var i = 0; i < count; i++)
        {
            items.Add(new SearchItem(
                Guid.NewGuid(),
                "movie",
                $"Title {i}",
                null,
                null,
                null,
                null,
                null,
                7.5m,
                100,
                null));
        }

        return items;
    }

    private static PaginatedResult<SearchItem> Paginated(List<SearchItem> items) =>
        new(items, 1, items.Count, items.Count, 1);

    private sealed class SharedDiscoveryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);
        private readonly object _sync = new();

        public int SetCountForPrefix(string prefix) =>
            _entries.Keys.Count(key => key.StartsWith(prefix, StringComparison.Ordinal));

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            lock (_sync)
            {
                return Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);
            }
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            lock (_sync)
            {
                _entries[key] = value!;
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _entries.Remove(key);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SlowTrendingRepository(TimeSpan delay) : ISearchRepository
    {
        private int _trendingLoadCount;

        public int TrendingLoadCount => _trendingLoadCount;

        public async Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _trendingLoadCount);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            return Paginated(CreateItems(1));
        }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PaginatedResult<SearchItem>> SearchAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<decimal> GetCatalogMeanVoteAverageAsync(
            SearchContentType type,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(
            IReadOnlyList<SearchItem> items,
            Guid genreId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithAnyGenreAsync(
            IReadOnlyList<SearchItem> items,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class DualSlowRepository(TimeSpan delay) : ISearchRepository
    {
        private int _trendingLoadCount;
        private int _popularLoadCount;

        public int TrendingLoadCount => _trendingLoadCount;

        public int PopularLoadCount => _popularLoadCount;

        public async Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _trendingLoadCount);
            await Task.Delay(delay, cancellationToken);
            return Paginated(CreateItems(1));
        }

        public async Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _popularLoadCount);
            await Task.Delay(delay, cancellationToken);
            return Paginated(CreateItems(1));
        }

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PaginatedResult<SearchItem>> SearchAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<decimal> GetCatalogMeanVoteAverageAsync(
            SearchContentType type,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(
            IReadOnlyList<SearchItem> items,
            Guid genreId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithAnyGenreAsync(
            IReadOnlyList<SearchItem> items,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
