using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class SearchServiceProviderFailureTests
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

    [Fact]
    public async Task SearchAsyncReturnsExistingCatalogWhenProviderFailsAndCatalogIsUsable()
    {
        var service = SearchServiceTestsHelper.CreateService(
            new SearchServiceTestsHelper.FakeSearchRepository([MovieItem], totalCount: 20),
            new SearchServiceTestsHelper.FakeCacheService(null),
            new SearchTestDoubles.FakeProviderIngestionService
            {
                MovieSucceeds = false,
                TvSucceeds = false
            },
            refreshRepository: CreateStaleRefreshRepository("inception"));

        var result = await service.SearchAsync(CreateCriteria("inception"));

        Assert.Equal(20, result.TotalCount);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("429")]
    [InlineData("5xx")]
    public async Task SearchAsyncThrows503WhenProviderFailsWithEmptyCatalog(string failureKind)
    {
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService
        {
            MovieSucceeds = false,
            TvSucceeds = false
        };

        if (failureKind == "timeout")
        {
            providerIngestion.ThrowBeforeResult = true;
        }

        var service = SearchServiceTestsHelper.CreateService(
            new SearchServiceTestsHelper.FakeSearchRepository([], totalCount: 0),
            new SearchServiceTestsHelper.FakeCacheService(null),
            providerIngestion);

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.SearchAsync(CreateCriteria("friends")));
    }

    [Fact]
    public async Task SearchAsyncConcurrentProviderFailuresDoNotAdvanceFreshnessOrCache()
    {
        var refreshRepository = CreateStaleRefreshRepository("friends");
        var cache = new SearchServiceTestsHelper.FakeCacheService(null);
        var providerIngestion = new SearchTestDoubles.FakeProviderIngestionService
        {
            MovieSucceeds = false,
            TvSucceeds = false
        };
        var service = SearchServiceTestsHelper.CreateService(
            new SearchServiceTestsHelper.FakeSearchRepository([], totalCount: 0),
            cache,
            providerIngestion,
            refreshRepository);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => RecordOutcomeAsync(service))
            .ToArray();

        var outcomes = await Task.WhenAll(tasks);

        Assert.All(outcomes, outcome =>
        {
            Assert.False(outcome.Succeeded);
            Assert.IsType<SearchProviderUnavailableException>(outcome.Error);
        });
        Assert.Equal(0, refreshRepository.SetCount);
        Assert.Equal(0, cache.SetCount);
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

    [Fact]
    public async Task SearchAsyncDoesNotAdvanceFreshnessWhenProviderFails()
    {
        var refreshRepository = CreateStaleRefreshRepository("friends");
        var service = SearchServiceTestsHelper.CreateService(
            new SearchServiceTestsHelper.FakeSearchRepository([], totalCount: 0),
            new SearchServiceTestsHelper.FakeCacheService(null),
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
    public async Task SearchAsyncDoesNotCacheFailedProviderRefresh()
    {
        var cache = new SearchServiceTestsHelper.FakeCacheService(null);
        var service = SearchServiceTestsHelper.CreateService(
            new SearchServiceTestsHelper.FakeSearchRepository([MovieItem], totalCount: 20),
            cache,
            new SearchTestDoubles.FakeProviderIngestionService
            {
                MovieSucceeds = true,
                TvSucceeds = false
            },
            refreshRepository: CreateStaleRefreshRepository("friends"));

        await service.SearchAsync(CreateCriteria("friends", SearchContentType.All));

        Assert.Equal(0, cache.SetCount);
    }

    [Fact]
    public async Task LockHolderPublishesFailedWhenUnexpectedExceptionOccursWithEmptyCatalog()
    {
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var completionSignal = new CapturingCompletionSignal();
        var provider = new SearchTestDoubles.FakeProviderIngestionService
        {
            ThrowBeforeResult = true
        };

        var service = SearchServiceTestsHelper.CreateService(
            new SearchServiceTestsHelper.FakeSearchRepository([], totalCount: 0),
            new SearchServiceTestsHelper.FakeCacheService(null),
            provider,
            lockService: lockService,
            completionSignal: completionSignal);

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.SearchAsync(CreateCriteria("unexpected failure")));

        Assert.Equal(SearchRefreshAttemptOutcome.Failed, completionSignal.LastPublishedOutcome);
        Assert.Equal(1, lockService.ReleaseCount);
    }

    [Fact]
    public async Task LockHolderPublishesFailedAndReturnsCatalogWhenUnexpectedExceptionOccursWithUsableCatalog()
    {
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var completionSignal = new CapturingCompletionSignal();
        var provider = new SearchTestDoubles.FakeProviderIngestionService();

        var service = SearchServiceTestsHelper.CreateService(
            new ThrowAfterRefreshSearchRepository(),
            new SearchServiceTestsHelper.FakeCacheService(null),
            provider,
            lockService: lockService,
            completionSignal: completionSignal);

        var result = await service.SearchAsync(CreateCriteria("catalog fallback"));

        Assert.Single(result.Items);
        Assert.Equal(SearchRefreshAttemptOutcome.Failed, completionSignal.LastPublishedOutcome);
    }

    [Fact]
    public async Task LockHolderPublishesSucceededOnSuccessfulRefresh()
    {
        var lockService = new SearchTestDoubles.InMemorySearchRefreshLockService();
        var completionSignal = new CapturingCompletionSignal();
        var provider = new SearchTestDoubles.FakeProviderIngestionService();

        var service = SearchServiceTestsHelper.CreateService(
            new SearchServiceTestsHelper.FakeSearchRepository([MovieItem], totalCount: 20),
            new SearchServiceTestsHelper.FakeCacheService(null),
            provider,
            refreshRepository: CreateStaleRefreshRepository("success path"),
            lockService: lockService,
            completionSignal: completionSignal);

        await service.SearchAsync(CreateCriteria("success path"));

        Assert.Equal(SearchRefreshAttemptOutcome.Succeeded, completionSignal.LastPublishedOutcome);
    }

    private static SearchTestDoubles.FakeSearchProviderRefreshRepository CreateStaleRefreshRepository(string query)
    {
        var repository = new SearchTestDoubles.FakeSearchProviderRefreshRepository();
        repository.Seed(query, SearchContentType.All, 1, DateTime.UtcNow.AddHours(-30));
        return repository;
    }

    private static SearchCriteria CreateCriteria(string query, SearchContentType type = SearchContentType.All) =>
        new(query, type, null, null, null, null, SearchSortOption.Relevance, 1, 20);

    private sealed class CapturingCompletionSignal : ISearchRefreshCompletionSignal
    {
        public SearchRefreshAttemptOutcome? LastPublishedOutcome { get; private set; }

        public Task PublishAsync(
            string lockKey,
            SearchRefreshAttemptOutcome outcome,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            LastPublishedOutcome = outcome;
            return Task.CompletedTask;
        }

        public Task<SearchRefreshAttemptOutcome?> TryGetOutcomeAsync(
            string lockKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LastPublishedOutcome);
    }

    private sealed class ThrowAfterRefreshSearchRepository : SearchServiceTestsHelper.FakeSearchRepository
    {
        private int _searchCount;

        public ThrowAfterRefreshSearchRepository()
            : base([], totalCount: 1)
        {
        }

        public override Task<PaginatedResult<SearchItem>> SearchAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            _searchCount++;

            if (_searchCount == 1)
            {
                return Task.FromResult(new PaginatedResult<SearchItem>(
                    [MovieItem],
                    criteria.Page,
                    criteria.PageSize,
                    1,
                    1));
            }

            throw new InvalidOperationException("database failure after refresh");
        }
    }
}

internal static class SearchServiceTestsHelper
{
    internal static SearchService CreateService(
        FakeSearchRepository repository,
        FakeCacheService cache,
        SearchTestDoubles.FakeProviderIngestionService providerIngestion,
        SearchTestDoubles.FakeSearchProviderRefreshRepository? refreshRepository = null,
        ISearchRefreshLockService? lockService = null,
        ISearchRefreshCompletionSignal? completionSignal = null) =>
        new(
            repository,
            new FakeSearchHistoryRepository(),
            refreshRepository ?? new SearchTestDoubles.FakeSearchProviderRefreshRepository(),
            new FakeCurrentUser(null),
            cache,
            providerIngestion,
            lockService ?? new SearchTestDoubles.InMemorySearchRefreshLockService(),
            completionSignal ?? SearchTestDoubles.CreateCompletionSignal(),
            SearchTestDoubles.CreateOptionsMonitor());

    internal sealed class FakeCurrentUser(Guid? userId) : MovieApp.Application.Abstractions.Identity.ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;
    }

    internal sealed class FakeSearchHistoryRepository : MovieApp.Application.Abstractions.Persistence.ISearchHistoryRepository
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

    internal class FakeSearchRepository : MovieApp.Application.Abstractions.Persistence.ISearchRepository
    {
        private readonly IReadOnlyList<SearchItem> _items;
        private readonly int _totalCount;
        protected readonly bool AlwaysEmptyAfterIngest;

        public FakeSearchRepository(
            IReadOnlyList<SearchItem> items,
            int totalCount,
            bool alwaysEmptyAfterIngest = false)
        {
            _items = items;
            _totalCount = totalCount;
            AlwaysEmptyAfterIngest = alwaysEmptyAfterIngest;
        }

        public virtual Task<PaginatedResult<SearchItem>> SearchAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            if (AlwaysEmptyAfterIngest)
            {
                return Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));
            }

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

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
            string genreName,
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));
    }

    internal sealed class FakeCacheService : MovieApp.Application.Abstractions.Caching.ICacheService
    {
        public int SetCount { get; private set; }

        public FakeCacheService(object? _) { }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
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
