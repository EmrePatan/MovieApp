using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MovieApp.UnitTests.Search;

public sealed class SearchServiceTests
{
    private static readonly SearchItem MovieItem = new(
        Guid.NewGuid(),
        "movie",
        "Inception",
        null,
        "Overview",
        "/poster.jpg",
        null,
        new DateOnly(2010, 7, 16),
        8.8m,
        32000,
        2010);

    private static readonly SearchItem TvItem = new(
        Guid.NewGuid(),
        "tv",
        "Friends",
        null,
        "Overview",
        "/poster.jpg",
        null,
        new DateOnly(1994, 9, 22),
        8.9m,
        12000,
        1994);

    [Fact]
    public async Task SearchAsyncReturnsCachedResultOnHitWithoutQueryingRepositoryOrProvider()
    {
        var cache = new FakeCacheService(new PaginatedResult<SearchItem>([MovieItem], 1, 20, 1, 1));
        var repository = new FakeSearchRepository();
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("inception"));

        Assert.Single(result.Items);
        Assert.Equal(0, repository.SearchCount);
        Assert.Equal(0, providerIngestion.IngestCount);
        Assert.Equal(1, cache.GetCount);
    }

    [Fact]
    public async Task SearchAsyncQueriesProviderFirstOnCacheMissWithoutQueryingDatabase()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 20);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("inception"));

        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(0, repository.SearchCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncReturnsProviderResultsWhenDatabaseIsEmpty()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("friends"));

        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(0, repository.SearchCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncAlwaysQueriesProviderOnCacheMissEvenWhenDatabaseHasFreshCatalog()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 20);
        var refreshRepository = new SearchTestDoubles.FakeSearchProviderRefreshRepository();
        refreshRepository.Seed("inception", SearchContentType.All, 1, DateTime.UtcNow.AddHours(-1));
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion, refreshRepository);

        await service.SearchAsync(CreateCriteria("inception"));

        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(0, repository.SearchCount);
    }

    [Fact]
    public async Task SearchAsyncUsesOnlyMovieProviderForMovieType()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("batman", SearchContentType.Movie));

        Assert.Equal(SearchContentType.Movie, providerIngestion.LastCriteria!.Type);
        Assert.Single(result.Items);
        Assert.Equal("movie", result.Items[0].Type);
    }

    [Fact]
    public async Task SearchAsyncUsesOnlyTvProviderForTvType()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("friends", SearchContentType.Tv));

        Assert.Equal(SearchContentType.Tv, providerIngestion.LastCriteria!.Type);
        Assert.Single(result.Items);
        Assert.Equal("tv", result.Items[0].Type);
    }

    [Fact]
    public async Task SearchAsyncMergesMovieAndTvResultsForAllType()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("friends", SearchContentType.All));

        Assert.Contains(result.Items, item => item.Type == "movie");
        Assert.Contains(result.Items, item => item.Type == "tv");
    }

    [Fact]
    public async Task SearchAsyncCachesEmptyResultsAfterSuccessfulProviderSearch()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var refreshRepository = new SearchTestDoubles.FakeSearchProviderRefreshRepository();
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService
        {
            MovieSucceeds = false,
            TvSucceeds = false
        };
        var service = CreateService(repository, cache, providerIngestion, refreshRepository);

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.SearchAsync(CreateCriteria("missing-title")));

        Assert.Equal(0, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncCachesSuccessfulProviderResultsIncludingLowTotals()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var refreshRepository = new SearchTestDoubles.FakeSearchProviderRefreshRepository();
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion, refreshRepository);

        await service.SearchAsync(CreateCriteria("missing-title"));

        Assert.Equal(1, refreshRepository.SetCount);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncFallsBackToDatabaseWhenProviderFails()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 20);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService
        {
            MovieSucceeds = false,
            TvSucceeds = false
        };
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("inception"));

        Assert.Equal(1, repository.SearchCount);
        Assert.Equal(0, cache.SetCount);
        Assert.Single(result.Items);
        Assert.Equal(20, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsyncWaiterReturnsShortlyAfterSuccessfulRefresh()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService
        {
            ArtificialDelayMilliseconds = 500
        };
        var service = CreateService(repository, cache, providerIngestion);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.SearchAsync(CreateCriteria("friends")))
            .ToArray();
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.True(stopwatch.ElapsedMilliseconds < 5_000);
    }

    [Fact]
    public async Task SearchAsyncWaiterReturnsShortlyAfterFailedRefresh()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService
        {
            MovieSucceeds = false,
            TvSucceeds = false,
            ArtificialDelayMilliseconds = 500
        };
        var service = CreateService(repository, cache, providerIngestion);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => RecordOutcomeAsync(service))
            .ToArray();
        var outcomes = await Task.WhenAll(tasks);
        stopwatch.Stop();

        Assert.All(outcomes, outcome => Assert.IsType<SearchProviderUnavailableException>(outcome.Error));
        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.True(stopwatch.ElapsedMilliseconds < 5_000);
    }

    [Fact]
    public async Task SearchAsyncConcurrentSuccessfulRefreshCallsProviderOnce()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var refreshRepository = new SearchTestDoubles.FakeSearchProviderRefreshRepository();
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService
        {
            ArtificialDelayMilliseconds = 500
        };
        var service = CreateService(repository, cache, providerIngestion, refreshRepository);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.SearchAsync(CreateCriteria("missing-title")))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(1, refreshRepository.SetCount);
    }

    [Fact]
    public async Task SearchAsyncDoesNotCacheFailedProviderRefresh()
    {
        var cache = new FakeCacheService(null);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService
        {
            MovieSucceeds = false,
            TvSucceeds = false
        };
        var service = CreateService(
            new FakeSearchRepository([], totalCount: 0),
            cache,
            providerIngestion);

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.SearchAsync(CreateCriteria("missing-title")));

        Assert.Equal(0, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncDoesNotAdvanceFreshnessWhenProviderFails()
    {
        var refreshRepository = new SearchTestDoubles.FakeSearchProviderRefreshRepository();
        var service = CreateService(
            new FakeSearchRepository([], totalCount: 0),
            new FakeCacheService(null),
            new SearchTestDoubles.FakeProviderIngestionService
            {
                MovieSucceeds = false,
                TvSucceeds = false
            },
            refreshRepository);

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.SearchAsync(CreateCriteria("friends")));

        Assert.Equal(0, refreshRepository.SetCount);
    }

    [Fact]
    public async Task SearchAsyncAdvancesFreshnessWhenProviderSucceeds()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 20);
        var refreshRepository = new SearchTestDoubles.FakeSearchProviderRefreshRepository();
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion, refreshRepository);

        await service.SearchAsync(CreateCriteria("friends", SearchContentType.All));

        Assert.Equal(1, refreshRepository.SetCount);
    }

    [Fact]
    public async Task SearchAsyncUsesDatabaseOnlyForFilteredCriteria()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 1);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var criteria = new SearchCriteria(
            "inception",
            SearchContentType.All,
            Guid.NewGuid(),
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        await service.SearchAsync(criteria);

        Assert.Equal(0, providerIngestion.IngestCount);
        Assert.Equal(1, repository.SearchCount);
    }

    [Fact]
    public async Task SearchAsyncPropagatesProviderFailureWhenCatalogCannotSatisfyPage()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService
        {
            MovieSucceeds = false,
            TvSucceeds = false
        };
        var service = CreateService(repository, cache, providerIngestion);

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.SearchAsync(CreateCriteria("friends")));
    }

    [Fact]
    public async Task SearchAsyncConcurrentStaleRequestsOnlyRefreshProviderOnce()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 20);
        var refreshRepository = new SearchTestDoubles.FakeSearchProviderRefreshRepository();
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var service = CreateService(repository, cache, providerIngestion, refreshRepository, lockService);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => service.SearchAsync(CreateCriteria("friends")))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(1, refreshRepository.SetCount);
    }

    [Fact]
    public async Task SearchAsyncWaitingRequestDoesNotCallProviderWhenLockIsHeld()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 20);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var options = SearchTestDoubles.CreateOptions(lockDuration: TimeSpan.FromSeconds(30));
        var service = CreateService(repository, cache, providerIngestion, lockService: lockService, options: options);

        var lockHandle = await lockService.TryAcquireAsync(
            SearchRefreshLockKeys.Create(CreateCriteria("friends")),
            options.ProviderRefreshLockDuration);

        Assert.NotNull(lockHandle);

        var waitingTask = service.SearchAsync(CreateCriteria("friends"));
        await cache.SetAsync(
            UnifiedSearchCacheKeys.Create(CreateCriteria("friends")),
            new UnifiedSearchCacheEntry
            {
                Result = new PaginatedResult<SearchItem>([MovieItem], 1, 20, 1, 1)
            },
            options.CacheDuration);
        await lockService.ReleaseAsync(lockHandle!.LockKey, lockHandle.LockToken, lockHandle.Backend);

        await waitingTask;

        Assert.Equal(0, providerIngestion.IngestCount);
    }

    [Fact]
    public async Task SearchAsyncUsesLocalSingleFlightWhenDistributedLockContends()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService
        {
            ArtificialDelayMilliseconds = 500
        };
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var service = CreateService(repository, cache, providerIngestion, lockService: lockService);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.SearchAsync(CreateCriteria("friends")))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(1, lockService.SuccessfulAcquires);
    }

    [Fact]
    public async Task SearchAsyncUsesConfiguredCacheDuration()
    {
        var options = SearchTestDoubles.CreateOptions(cacheDuration: TimeSpan.FromMinutes(30));
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 20);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion, options: options);

        await service.SearchAsync(CreateCriteria("inception"));

        Assert.Equal(TimeSpan.FromMinutes(30), cache.LastExpiry);
    }

    [Fact]
    public async Task SearchAsyncThrowsForInvalidCriteria()
    {
        var service = CreateService(
            new FakeSearchRepository(),
            new FakeCacheService(null),
            new SearchTestDoubles.FakeProviderIngestionService());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SearchAsync(CreateCriteria("a")));
    }

    private static async Task<(bool Succeeded, Exception? Error)> RecordOutcomeAsync(SearchService service)
    {
        try
        {
            await service.SearchAsync(CreateCriteria("friends"));
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception);
        }
    }

    private static SearchService CreateService(
        FakeSearchRepository repository,
        FakeCacheService cache,
        SearchTestDoubles.FakeProviderIngestionService providerIngestion,
        SearchTestDoubles.FakeSearchProviderRefreshRepository? refreshRepository = null,
        ISearchRefreshLockService? lockService = null,
        ISearchRefreshCompletionSignal? completionSignal = null,
        SearchOptions? options = null) =>
        new(
            repository,
            new FakeSearchHistoryRepository(),
            refreshRepository ?? new SearchTestDoubles.FakeSearchProviderRefreshRepository(),
            new FakeCurrentUser(null),
            cache,
            providerIngestion,
            lockService ?? new SearchTestDoubles.InMemorySearchRefreshLockService(),
            completionSignal ?? SearchTestDoubles.CreateCompletionSignal(),
            SearchTestDoubles.CreateOptionsMonitor(options),
            NullLogger<SearchService>.Instance);

    private static SearchCriteria CreateCriteria(
        string query,
        SearchContentType type = SearchContentType.All,
        int page = 1,
        int pageSize = 20) =>
        new(query, type, null, null, null, null, SearchSortOption.Relevance, page, pageSize);

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;
    }

    private sealed class FakeSearchRepository : ISearchRepository
    {
        private readonly IReadOnlyList<SearchItem> _items;
        private readonly int _totalCount;

        public FakeSearchRepository(
            IReadOnlyList<SearchItem>? items = null,
            int totalCount = 1)
        {
            _items = items ?? [MovieItem];
            _totalCount = totalCount;
        }

        public int SearchCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> SearchAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            SearchCount++;

            return Task.FromResult(new PaginatedResult<SearchItem>(
                _items,
                criteria.Page,
                criteria.PageSize,
                _totalCount,
                Math.Max(1, (int)Math.Ceiling(_totalCount / (double)criteria.PageSize))));
        }

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchSuggestion>>([]);

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

        public Task<decimal> GetCatalogMeanVoteAverageAsync(
            SearchContentType type,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(6.0m);

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));
    }

    private sealed class FakeSearchHistoryRepository : ISearchHistoryRepository
    {
        public Task RecordSearchAsync(
            Guid userId,
            string query,
            string normalizedQuery,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(IReadOnlyList<MovieApp.Domain.Entities.SearchHistory> Items, int TotalCount)> GetUserHistoryAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<MovieApp.Domain.Entities.SearchHistory>, int)>(([], 0));

        public Task<bool> DeleteAsync(Guid userId, Guid historyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeCacheService : ICacheService
    {
        private PaginatedResult<SearchItem>? _cachedResult;

        public FakeCacheService(PaginatedResult<SearchItem>? cachedResult)
        {
            _cachedResult = cachedResult;
        }

        public int GetCount { get; private set; }

        public int SetCount { get; private set; }

        public TimeSpan? LastExpiry { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            GetCount++;
            if (_cachedResult is not null && typeof(T) == typeof(UnifiedSearchCacheEntry))
            {
                return Task.FromResult<T?>((T)(object)new UnifiedSearchCacheEntry { Result = _cachedResult });
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class
        {
            SetCount++;
            LastExpiry = expiry;

            if (value is UnifiedSearchCacheEntry entry)
            {
                _cachedResult = entry.Result;
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
