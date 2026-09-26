using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoveryServiceInProcessWaiterTests
{
    [Fact]
    public async Task ConcurrentMissesJoinInProcessLoadWithoutDuplicateCanonicalWork()
    {
        var coordinator = new DiscoveryCacheLoadCoordinator();
        var cache = new SharedDiscoveryCacheService();
        var repository = new SlowTrendingRepository(TimeSpan.FromMilliseconds(300));
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var service = new DiscoveryService(
            repository,
            cache,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            lockService,
            coordinator,
            NullLogger<DiscoveryService>.Instance);

        var criteria = new DiscoveryCriteria(SearchContentType.All, 1, 10);
        var tasks = Enumerable.Range(0, 6)
            .Select(_ => service.GetTrendingAsync(criteria, ContentLocaleResolver.EnglishUnitedStates))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, repository.TrendingLoadCount);
    }

    private sealed class SharedDiscoveryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
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
            await Task.Delay(delay, cancellationToken);
            return new PaginatedResult<SearchItem>(
                [new SearchItem(Guid.NewGuid(), "movie", "Title", null, null, null, null, null, 8m, 10, null)],
                1,
                1,
                1,
                1);
        }

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(string query, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<decimal> GetCatalogMeanVoteAverageAsync(SearchContentType type, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(IReadOnlyList<SearchItem> items, Guid genreId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithAnyGenreAsync(IReadOnlyList<SearchItem> items, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
