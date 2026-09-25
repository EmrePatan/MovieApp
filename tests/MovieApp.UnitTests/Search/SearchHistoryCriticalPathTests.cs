using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Search;

public sealed class SearchHistoryCriticalPathTests
{
    [Fact]
    public async Task SearchAsync_ReturnsBeforeSearchHistoryWriteCompletes()
    {
        var history = new BlockingSearchHistoryRepository();
        var cached = new PaginatedResult<SearchItem>(
            [
                new SearchItem(
                    Guid.NewGuid(),
                    "movie",
                    "Inception",
                    null,
                    null,
                    null,
                    null,
                    null,
                    8m,
                    10,
                    2010,
                    27205)
            ],
            1,
            20,
            1,
            1);
        var service = new SearchService(
            new NoOpSearchRepository(),
            history,
            new SearchTestDoubles.FakeSearchProviderRefreshRepository(),
            new AuthenticatedUser(Guid.NewGuid()),
            new HitCacheService(cached),
            new SearchTestDoubles.FakeProviderIngestionService(),
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            new SearchTestDoubles.InMemorySearchRefreshLockService(),
            SearchTestDoubles.CreateCompletionSignal(),
            SearchTestDoubles.CreateOptionsMonitor(null),
            NullLogger<SearchService>.Instance,
            new SingleHistoryScopeFactory(history));

        var search = service.SearchAsync(
            new SearchCriteria(
                "inception",
                SearchContentType.Movie,
                null,
                null,
                null,
                null,
                SearchSortOption.Relevance,
                1,
                20),
            ContentLocaleResolver.EnglishUnitedStates);
        var completed = await Task.WhenAny(search, Task.Delay(TimeSpan.FromMilliseconds(500)));

        Assert.Same(search, completed);
        await search;
        await history.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        history.Release();
    }

    private sealed class AuthenticatedUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class HitCacheService(PaginatedResult<SearchItem> result) : MovieApp.Application.Abstractions.Caching.ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (typeof(T) == typeof(UnifiedSearchCacheEntry))
            {
                return Task.FromResult<T?>((T)(object)new UnifiedSearchCacheEntry { Result = result });
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpSearchRepository : ISearchRepository
    {
        public Task<PaginatedResult<SearchItem>> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SearchSuggestion>> AutocompleteAsync(string query, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetPopularAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetNewReleasesAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetTopRatedAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<decimal> GetCatalogMeanVoteAverageAsync(SearchContentType type, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(IReadOnlyList<SearchItem> items, Guid genreId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithAnyGenreAsync(IReadOnlyList<SearchItem> items, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PaginatedResult<SearchItem>> GetByGenreAsync(string genreName, DiscoveryCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class BlockingSearchHistoryRepository : ISearchHistoryRepository
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RecordSearchAsync(
            Guid userId,
            string query,
            string normalizedQuery,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            return _release.Task;
        }

        public void Release() => _release.TrySetResult();

        public Task<(IReadOnlyList<SearchHistory> Items, int TotalCount)> GetUserHistoryAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid userId, Guid historyId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SingleHistoryScopeFactory(ISearchHistoryRepository repository) : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            var services = new ServiceCollection();
            services.AddSingleton(repository);
            return services.BuildServiceProvider().CreateScope();
        }
    }
}
