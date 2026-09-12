using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

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
        var cache = new FakeCacheService(
            new PaginatedResult<SearchItem>([MovieItem], 1, 20, 1, 1));
        var repository = new FakeSearchRepository();
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("inception"));

        Assert.Single(result.Items);
        Assert.Equal(0, repository.SearchCount);
        Assert.Equal(0, providerIngestion.IngestCount);
        Assert.Equal(1, cache.GetCount);
    }

    [Fact]
    public async Task SearchAsyncQueriesRepositoryOnCacheMissAndCachesWhenCatalogIsSufficient()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 20);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("inception"));

        Assert.Single(result.Items);
        Assert.Equal(1, repository.SearchCount);
        Assert.Equal(0, providerIngestion.IngestCount);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncCallsProviderWhenCatalogHasNoResults()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("friends"));

        Assert.Equal(2, repository.SearchCount);
        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(SearchContentType.All, providerIngestion.LastCriteria!.Type);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncCallsProviderWhenCatalogHasInsufficientResultsForFirstPage()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 5);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("batman"));

        Assert.Equal(2, repository.SearchCount);
        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncDoesNotCallProviderWhenLaterPageIsPartialButNonEmpty()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 25, page: 2);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var criteria = CreateCriteria("inception", SearchContentType.All, page: 2, pageSize: 20);
        var result = await service.SearchAsync(criteria);

        Assert.Single(result.Items);
        Assert.Equal(1, repository.SearchCount);
        Assert.Equal(0, providerIngestion.IngestCount);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncCallsProviderWhenRequestedPageWouldBeEmpty()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0, page: 2);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var criteria = CreateCriteria("batman", SearchContentType.All, page: 2, pageSize: 20);
        var result = await service.SearchAsync(criteria);

        Assert.Equal(2, repository.SearchCount);
        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task SearchAsyncUsesOnlyMovieProviderForMovieType()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        await service.SearchAsync(CreateCriteria("batman", SearchContentType.Movie));

        Assert.Equal(SearchContentType.Movie, providerIngestion.LastCriteria!.Type);
    }

    [Fact]
    public async Task SearchAsyncUsesOnlyTvProviderForTvType()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        await service.SearchAsync(CreateCriteria("friends", SearchContentType.Tv));

        Assert.Equal(SearchContentType.Tv, providerIngestion.LastCriteria!.Type);
    }

    [Fact]
    public async Task SearchAsyncMergesMovieAndTvResultsForAllType()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("friends", SearchContentType.All));

        Assert.Contains(result.Items, item => item.Type == "movie");
        Assert.Contains(result.Items, item => item.Type == "tv");
    }

    [Fact]
    public async Task SearchAsyncReturnsBothMediaTypesWhenSameTitleExistsAsMovieAndTv()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("batman", SearchContentType.All));

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, item => item.Type == "movie" && item.Title == "Batman");
        Assert.Contains(result.Items, item => item.Type == "tv" && item.Title == "Batman");
    }

    [Fact]
    public async Task SearchAsyncDoesNotCacheEmptyResultsAfterProviderFallback()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0, alwaysEmptyAfterIngest: true);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("missing-title"));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(0, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncUsesDistinctCacheKeysForDifferentPaginationAndType()
    {
        var criteriaA = CreateCriteria("friends", SearchContentType.All, page: 1, pageSize: 20);
        var criteriaB = CreateCriteria("friends", SearchContentType.Movie, page: 1, pageSize: 20);
        var criteriaC = CreateCriteria("friends", SearchContentType.All, page: 2, pageSize: 20);

        Assert.NotEqual(UnifiedSearchCacheKeys.Create(criteriaA), UnifiedSearchCacheKeys.Create(criteriaB));
        Assert.NotEqual(UnifiedSearchCacheKeys.Create(criteriaA), UnifiedSearchCacheKeys.Create(criteriaC));
    }

    [Theory]
    [InlineData("Inception")]
    [InlineData("Avatar")]
    [InlineData("Breaking Bad")]
    [InlineData("The Matrix")]
    [InlineData("Titanic")]
    public async Task SearchAsyncDoesNotCallProviderWhenCatalogAlreadyContainsKnownTitles(string query)
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 20);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        await service.SearchAsync(CreateCriteria(query));

        Assert.Equal(0, providerIngestion.IngestCount);
    }

    [Theory]
    [InlineData("Friends")]
    [InlineData("Batman")]
    public async Task SearchAsyncFallsBackToProviderForTitlesMissingFromCatalog(string query)
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new FakeProviderIngestionService();
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria(query, SearchContentType.All));

        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.True(result.Items.Count >= 2);
    }

    [Fact]
    public async Task SearchAsyncReturnsPartialCatalogResultsWhenProviderFailsAndCatalogIsInsufficient()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([MovieItem], totalCount: 5);
        var providerIngestion = new FakeProviderIngestionService(throwOnIngest: true);
        var service = CreateService(repository, cache, providerIngestion);

        var result = await service.SearchAsync(CreateCriteria("inception"));

        Assert.Single(result.Items);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, providerIngestion.IngestCount);
        Assert.Equal(0, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncPropagatesProviderFailureWhenCatalogCannotSatisfyPage()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository([], totalCount: 0);
        var providerIngestion = new FakeProviderIngestionService(throwOnIngest: true);
        var service = CreateService(repository, cache, providerIngestion);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SearchAsync(CreateCriteria("friends")));
    }

    [Fact]
    public async Task SearchAsyncRecordsHistoryForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var historyRepository = new FakeSearchHistoryRepository();
        var service = new SearchService(
            new FakeSearchRepository([MovieItem], totalCount: 20),
            historyRepository,
            new FakeCurrentUser(userId),
            new FakeCacheService(null),
            new FakeProviderIngestionService());

        await service.SearchAsync(CreateCriteria("batman"));

        Assert.Equal(1, historyRepository.RecordCount);
    }

    [Fact]
    public async Task SearchAsyncThrowsForInvalidCriteria()
    {
        var service = CreateService(
            new FakeSearchRepository(),
            new FakeCacheService(null),
            new FakeProviderIngestionService());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SearchAsync(CreateCriteria("a")));
    }

    private static SearchService CreateService(
        FakeSearchRepository repository,
        FakeCacheService cache,
        FakeProviderIngestionService providerIngestion) =>
        new(
            repository,
            new FakeSearchHistoryRepository(),
            new FakeCurrentUser(null),
            cache,
            providerIngestion);

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
        private readonly int _page;
        private readonly bool _alwaysEmptyAfterIngest;

        public FakeSearchRepository(
            IReadOnlyList<SearchItem>? items = null,
            int totalCount = 1,
            int page = 1,
            bool alwaysEmptyAfterIngest = false)
        {
            _items = items ?? [MovieItem];
            _totalCount = totalCount;
            _page = page;
            _alwaysEmptyAfterIngest = alwaysEmptyAfterIngest;
        }

        public int SearchCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> SearchAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            SearchCount++;

            if (SearchCount > 1 && (_alwaysEmptyAfterIngest || _totalCount == 0 || _totalCount < criteria.PageSize))
            {
                if (_alwaysEmptyAfterIngest)
                {
                    return Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));
                }

                var title = QueryTitle(criteria.Query);
                return Task.FromResult(new PaginatedResult<SearchItem>(
                    [CreateItem("movie", title), CreateItem("tv", title)],
                    criteria.Page,
                    criteria.PageSize,
                    2,
                    1));
            }

            var page = SearchCount == 1 && _page != 1 ? _page : criteria.Page;
            return Task.FromResult(new PaginatedResult<SearchItem>(
                _items,
                page,
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

        private static string QueryTitle(string? query) =>
            string.IsNullOrWhiteSpace(query)
                ? "Result"
                : char.ToUpperInvariant(query[0]) + query[1..];

        private static SearchItem CreateItem(string type, string title) =>
            type == "movie"
                ? MovieItem with { Id = Guid.NewGuid(), Type = type, Title = title }
                : TvItem with { Id = Guid.NewGuid(), Type = type, Title = title };
    }

    private sealed class FakeSearchHistoryRepository : ISearchHistoryRepository
    {
        public int RecordCount { get; private set; }

        public Task RecordSearchAsync(
            Guid userId,
            string query,
            string normalizedQuery,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            RecordCount++;
            return Task.CompletedTask;
        }

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

    private sealed class FakeCacheService(PaginatedResult<SearchItem>? cachedResult) : ICacheService
    {
        public int GetCount { get; private set; }

        public int SetCount { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            GetCount++;
            if (cachedResult is not null && typeof(T) == typeof(UnifiedSearchCacheEntry))
            {
                return Task.FromResult<T?>((T)(object)new UnifiedSearchCacheEntry
                {
                    Result = cachedResult
                });
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class
        {
            SetCount++;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeProviderIngestionService(bool throwOnIngest = false) : IUnifiedSearchProviderIngestionService
    {
        public int IngestCount { get; private set; }

        public SearchCriteria? LastCriteria { get; private set; }

        public Task IngestAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            IngestCount++;
            LastCriteria = criteria;

            if (throwOnIngest)
            {
                throw new InvalidOperationException("provider unavailable");
            }

            return Task.CompletedTask;
        }
    }
}
