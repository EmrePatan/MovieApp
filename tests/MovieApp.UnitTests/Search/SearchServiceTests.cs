using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class SearchServiceTests
{
    private static readonly SearchItem SampleItem = new(
        Guid.NewGuid(),
        "movie",
        "Interstellar",
        null,
        "Overview",
        "/poster.jpg",
        null,
        new DateOnly(2014, 11, 7),
        8.6m,
        32000,
        2014);

    [Fact]
    public async Task SearchAsyncReturnsCachedResultOnHit()
    {
        var cache = new FakeCacheService(
            new PaginatedResult<SearchItem>([SampleItem], 1, 20, 1, 1));
        var repository = new FakeSearchRepository();
        var service = new SearchService(repository, new FakeSearchHistoryRepository(), new FakeCurrentUser(null), cache);

        var result = await service.SearchAsync(CreateCriteria("interstellar"));

        Assert.Single(result.Items);
        Assert.Equal(0, repository.SearchCount);
        Assert.Equal(1, cache.GetCount);
    }

    [Fact]
    public async Task SearchAsyncQueriesRepositoryOnCacheMiss()
    {
        var cache = new FakeCacheService(null);
        var repository = new FakeSearchRepository();
        var service = new SearchService(repository, new FakeSearchHistoryRepository(), new FakeCurrentUser(null), cache);

        var result = await service.SearchAsync(CreateCriteria("interstellar"));

        Assert.Single(result.Items);
        Assert.Equal(1, repository.SearchCount);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task SearchAsyncRecordsHistoryForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var historyRepository = new FakeSearchHistoryRepository();
        var service = new SearchService(
            new FakeSearchRepository(),
            historyRepository,
            new FakeCurrentUser(userId),
            new FakeCacheService(null));

        await service.SearchAsync(CreateCriteria("batman"));

        Assert.Equal(1, historyRepository.RecordCount);
    }

    [Fact]
    public async Task SearchAsyncThrowsForInvalidCriteria()
    {
        var service = new SearchService(
            new FakeSearchRepository(),
            new FakeSearchHistoryRepository(),
            new FakeCurrentUser(null),
            new FakeCacheService(null));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SearchAsync(CreateCriteria("a")));
    }

    private static SearchCriteria CreateCriteria(string query) =>
        new(query, SearchContentType.All, null, null, null, null, SearchSortOption.Relevance, 1, 20);

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUser
    {
        public bool IsAuthenticated => userId is not null;

        public Guid? UserId => userId;
    }

    private sealed class FakeSearchRepository : ISearchRepository
    {
        public int SearchCount { get; private set; }

        public Task<PaginatedResult<SearchItem>> SearchAsync(
            SearchCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            SearchCount++;
            return Task.FromResult(new PaginatedResult<SearchItem>([SampleItem], 1, 20, 1, 1));
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
            if (cachedResult is not null && typeof(T) == typeof(Application.Caching.UnifiedSearchCacheEntry))
            {
                return Task.FromResult<T?>((T)(object)new Application.Caching.UnifiedSearchCacheEntry
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
}
