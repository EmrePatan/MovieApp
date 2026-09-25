using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Search;

public sealed class DiscoveryServiceCacheInstrumentationTests
{
    [Fact]
    public async Task GetTrendingAsyncOnCacheMissLogsDiscoveryPerfLoadCompleted()
    {
        var logger = new CollectingLogger<DiscoveryService>();
        var items = CreateItems(2);
        var repository = new StubSearchRepository { TrendingResult = Paginated(items) };
        var cache = new RecordingCacheService();
        var service = CreateService(repository, cache, logger);

        var result = await service.GetTrendingAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, 10),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, repository.TrendingCallCount);
        Assert.Single(cache.SetCalls);
        Assert.Contains(
            logger.Messages,
            message => message.Contains("DiscoveryPerf Cache=LOAD_COMPLETED", StringComparison.Ordinal)
                && message.Contains("Operation=Trending", StringComparison.Ordinal)
                && message.Contains("CanonicalLoadMs=", StringComparison.Ordinal)
                && message.Contains("ItemCount=2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetTrendingAsyncOnCacheHitDoesNotLogDiscoveryPerf()
    {
        var logger = new CollectingLogger<DiscoveryService>();
        var cached = Paginated(CreateItems(1));
        var cache = new RecordingCacheService(
            new DiscoveryCacheEntry { Result = cached });
        var repository = new StubSearchRepository();
        var service = CreateService(repository, cache, logger);

        await service.GetTrendingAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, 10),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(0, repository.TrendingCallCount);
        Assert.Empty(cache.SetCalls);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("DiscoveryPerf", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetTrendingAsyncWhenCanonicalLoadFailsLogsDiscoveryPerfLoadFailed()
    {
        var logger = new CollectingLogger<DiscoveryService>();
        var repository = new StubSearchRepository { ThrowOnTrending = true };
        var service = CreateService(repository, new RecordingCacheService(), logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetTrendingAsync(
                new DiscoveryCriteria(SearchContentType.All, 1, 10),
                ContentLocaleResolver.EnglishUnitedStates));

        Assert.Contains(
            logger.Messages,
            message => message.Contains("DiscoveryPerf Cache=LOAD_FAILED", StringComparison.Ordinal)
                && message.Contains("FailurePhase=Canonical", StringComparison.Ordinal)
                && message.Contains("ExceptionType=InvalidOperationException", StringComparison.Ordinal));
    }

    private static DiscoveryService CreateService(
        ISearchRepository repository,
        ICacheService cache,
        ILogger<DiscoveryService> logger) =>
        new(
            repository,
            cache,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            logger);

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

    private static PaginatedResult<SearchItem> Paginated(IReadOnlyList<SearchItem> items) =>
        new(items, 1, items.Count, items.Count, 1);

    private sealed class StubSearchRepository : ISearchRepository
    {
        public PaginatedResult<SearchItem> TrendingResult { get; init; } =
            new([], 1, 10, 0, 0);

        public int TrendingCallCount { get; private set; }

        public bool ThrowOnTrending { get; init; }

        public Task<PaginatedResult<SearchItem>> GetTrendingAsync(
            DiscoveryCriteria criteria,
            CancellationToken cancellationToken = default)
        {
            TrendingCallCount++;
            if (ThrowOnTrending)
            {
                throw new InvalidOperationException("Trending load failed.");
            }

            return Task.FromResult(TrendingResult);
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

    private sealed class RecordingCacheService : ICacheService
    {
        private readonly DiscoveryCacheEntry? _seeded;

        public List<(string Key, TimeSpan? Ttl)> SetCalls { get; } = [];

        public RecordingCacheService(DiscoveryCacheEntry? seeded = null)
        {
            _seeded = seeded;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_seeded is T seeded ? seeded : null);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            SetCalls.Add((key, expiry));
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
