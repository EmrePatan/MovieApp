using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Search;
using MovieApp.Infrastructure.Caching;

namespace MovieApp.LoadTests;

[Trait("Category", "Load")]
public sealed class SearchLoadConcurrencyTests
{
    [Fact]
    public async Task ScenarioA_SameStaleSearch_100ConcurrentRequests()
    {
        var environment = LoadTestEnvironment.CreateStaleCatalog(query: "batman");
        var metrics = await LoadTestRunner.ExecuteAsync(
            concurrency: 100,
            () => environment.Service.SearchAsync(environment.Criteria));

        environment.OutputScenarioSummary("Scenario A - same stale search", metrics);

        Assert.Equal(100, metrics.TotalRequests);
        Assert.Equal(100, metrics.SuccessfulRequests);
        Assert.Equal(0, metrics.FailedRequests);
        Assert.Equal(1, environment.ProviderIngestionCount);
        Assert.Equal(1, environment.LockDiagnostics.LocalAcquireSuccess);
        Assert.True(metrics.P95LatencyMs < 5_000, $"p95={metrics.P95LatencyMs}");
    }

    [Fact]
    public async Task ScenarioB_SameUncachedEmptyCatalog_100ConcurrentRequests()
    {
        var environment = LoadTestEnvironment.CreateEmptyCatalog(query: "friends");
        var metrics = await LoadTestRunner.ExecuteAsync(
            concurrency: 100,
            () => environment.Service.SearchAsync(environment.Criteria));

        environment.OutputScenarioSummary("Scenario B - same cache miss", metrics);

        Assert.Equal(100, metrics.TotalRequests);
        Assert.Equal(100, metrics.SuccessfulRequests);
        Assert.Equal(1, environment.ProviderIngestionCount);
        Assert.True(metrics.P95LatencyMs < 5_000, $"p95={metrics.P95LatencyMs}");
    }

    [Fact]
    public async Task ScenarioC_DifferentQueries_100ConcurrentRequests()
    {
        var environment = LoadTestEnvironment.CreateStaleCatalog(query: "unused");
        var metrics = await LoadTestRunner.ExecuteAsync(
            concurrency: 100,
            index =>
            {
                var criteria = new SearchCriteria(
                    $"query-{index}",
                    SearchContentType.All,
                    null,
                    null,
                    null,
                    null,
                    SearchSortOption.Relevance,
                    1,
                    20);

                return environment.Service.SearchAsync(criteria);
            });

        environment.OutputScenarioSummary("Scenario C - different queries", metrics);

        Assert.Equal(100, metrics.TotalRequests);
        Assert.Equal(100, metrics.SuccessfulRequests);
        Assert.Equal(100, environment.ProviderIngestionCount);
        Assert.Equal(100, environment.LockDiagnostics.LocalAcquireSuccess);
    }

    [Fact]
    public async Task ScenarioD_RedisUnavailableSameStaleSearch_100ConcurrentRequests()
    {
        var environment = LoadTestEnvironment.CreateStaleCatalog(query: "matrix");
        var metrics = await LoadTestRunner.ExecuteAsync(
            concurrency: 100,
            () => environment.Service.SearchAsync(environment.Criteria));

        environment.OutputScenarioSummary("Scenario D - local single-flight fallback", metrics);

        Assert.Equal(100, metrics.TotalRequests);
        Assert.Equal(100, metrics.SuccessfulRequests);
        Assert.Equal(1, environment.ProviderIngestionCount);
        Assert.Equal(1, environment.LockDiagnostics.LocalAcquireSuccess);
        Assert.True(metrics.P95LatencyMs < 5_000, $"p95={metrics.P95LatencyMs}");
    }

    [Fact]
    public async Task ScenarioE_SlowProvider_50ConcurrentRequests()
    {
        var environment = LoadTestEnvironment.CreateEmptyCatalog(query: "slow", providerDelayMs: 2_000);
        var metrics = await LoadTestRunner.ExecuteAsync(
            concurrency: 50,
            () => environment.Service.SearchAsync(environment.Criteria));

        environment.OutputScenarioSummary("Scenario E - slow provider", metrics);

        Assert.Equal(50, metrics.TotalRequests);
        Assert.Equal(50, metrics.SuccessfulRequests);
        Assert.Equal(1, environment.ProviderIngestionCount);
        Assert.True(metrics.P95LatencyMs < 5_000, $"p95={metrics.P95LatencyMs}");
        Assert.True(metrics.MaxLatencyMs < 5_000, $"max={metrics.MaxLatencyMs}");
    }

    [Fact]
    public async Task ScenarioF_ProviderFailureUnderConcurrency_100Requests()
    {
        var environment = LoadTestEnvironment.CreateEmptyCatalogWithFailingProvider(query: "fail");
        var metrics = await LoadTestRunner.ExecuteAsync(
            concurrency: 100,
            async () =>
            {
                try
                {
                    await environment.Service.SearchAsync(environment.Criteria);
                    return false;
                }
                catch (SearchProviderUnavailableException)
                {
                    return true;
                }
            });

        environment.OutputScenarioSummary("Scenario F - provider failure", metrics);

        Assert.Equal(100, metrics.TotalRequests);
        Assert.Equal(100, metrics.SuccessfulRequests);
        Assert.Equal(0, metrics.FailedRequests);
        Assert.Equal(1, environment.ProviderIngestionCount);
        Assert.Equal(0, environment.RefreshRepositorySetCount);
        Assert.True(metrics.P95LatencyMs < 5_000, $"p95={metrics.P95LatencyMs}");
    }

    [Fact]
    public async Task ScenarioG_FreshEmptyResultSequentialRequests_DoNotCallProvider()
    {
        var environment = LoadTestEnvironment.CreateFreshEmptyCatalog(query: "nonexistent-title");
        var metrics = await LoadTestRunner.ExecuteSequentialAsync(
            requestCount: 20,
            () => environment.Service.SearchAsync(environment.Criteria));

        environment.OutputScenarioSummary("Scenario G - fresh empty repeat", metrics);

        Assert.Equal(20, metrics.TotalRequests);
        Assert.Equal(20, metrics.SuccessfulRequests);
        Assert.Equal(0, environment.ProviderIngestionCount);
    }
}

internal static class LoadTestRunner
{
    public static async Task<LoadTestMetrics> ExecuteAsync(
        int concurrency,
        Func<Task<bool>> action)
    {
        var latencies = new long[concurrency];
        var failures = 0;

        var tasks = Enumerable.Range(0, concurrency).Select(async index =>
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (!await action())
                {
                    Interlocked.Increment(ref failures);
                }
            }
            catch
            {
                Interlocked.Increment(ref failures);
            }
            finally
            {
                stopwatch.Stop();
                latencies[index] = stopwatch.ElapsedMilliseconds;
            }
        });

        await Task.WhenAll(tasks);
        return LoadTestMetrics.FromLatencies(concurrency, failures, latencies);
    }

    public static async Task<LoadTestMetrics> ExecuteAsync(
        int concurrency,
        Func<Task<PaginatedResult<SearchItem>>> action)
    {
        var latencies = new long[concurrency];
        var failures = 0;

        var tasks = Enumerable.Range(0, concurrency).Select(async index =>
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _ = await action();
            }
            catch
            {
                Interlocked.Increment(ref failures);
            }
            finally
            {
                stopwatch.Stop();
                latencies[index] = stopwatch.ElapsedMilliseconds;
            }
        });

        await Task.WhenAll(tasks);
        return LoadTestMetrics.FromLatencies(concurrency, failures, latencies);
    }

    public static async Task<LoadTestMetrics> ExecuteAsync(
        int concurrency,
        Func<int, Task<PaginatedResult<SearchItem>>> action)
    {
        var latencies = new long[concurrency];
        var failures = 0;

        var tasks = Enumerable.Range(0, concurrency).Select(async index =>
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                _ = await action(index);
            }
            catch
            {
                Interlocked.Increment(ref failures);
            }
            finally
            {
                stopwatch.Stop();
                latencies[index] = stopwatch.ElapsedMilliseconds;
            }
        });

        await Task.WhenAll(tasks);
        return LoadTestMetrics.FromLatencies(concurrency, failures, latencies);
    }

    public static async Task<LoadTestMetrics> ExecuteSequentialAsync(
        int requestCount,
        Func<Task> action)
    {
        var latencies = new long[requestCount];
        var failures = 0;

        for (var index = 0; index < requestCount; index++)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await action();
            }
            catch
            {
                Interlocked.Increment(ref failures);
            }
            finally
            {
                stopwatch.Stop();
                latencies[index] = stopwatch.ElapsedMilliseconds;
            }
        }

        return LoadTestMetrics.FromLatencies(requestCount, failures, latencies);
    }
}

internal sealed record LoadTestMetrics(
    int TotalRequests,
    int SuccessfulRequests,
    int FailedRequests,
    double AverageLatencyMs,
    double P95LatencyMs,
    long MaxLatencyMs)
{
    public static LoadTestMetrics FromLatencies(int totalRequests, int failures, long[] latencies)
    {
        Array.Sort(latencies);
        var average = latencies.Average();
        var p95Index = Math.Min(latencies.Length - 1, (int)Math.Ceiling(latencies.Length * 0.95) - 1);

        return new LoadTestMetrics(
            totalRequests,
            totalRequests - failures,
            failures,
            average,
            latencies[p95Index],
            latencies[^1]);
    }
}

internal sealed class LoadTestEnvironment
{
    private readonly LoadTestRefreshRepository? _refreshRepository;

    private LoadTestEnvironment(
        SearchService service,
        SearchCriteria criteria,
        LoadTestProviderIngestionService providerIngestionService,
        SearchRefreshLockDiagnostics lockDiagnostics,
        LoadTestRefreshRepository? refreshRepository = null)
    {
        Service = service;
        Criteria = criteria;
        ProviderIngestionService = providerIngestionService;
        LockDiagnostics = lockDiagnostics;
        _refreshRepository = refreshRepository;
    }

    public SearchService Service { get; }

    public SearchCriteria Criteria { get; }

    public LoadTestProviderIngestionService ProviderIngestionService { get; }

    public SearchRefreshLockDiagnostics LockDiagnostics { get; }

    public int ProviderIngestionCount => ProviderIngestionService.IngestCount;

    public int RefreshRepositorySetCount => _refreshRepository?.SetCount ?? 0;

    public static LoadTestEnvironment CreateStaleCatalog(string query)
    {
        var refreshRepository = new LoadTestRefreshRepository();
        refreshRepository.Seed(query, SearchContentType.All, 1, DateTime.UtcNow.AddHours(-30));

        return Create(
            query,
            new LoadTestSearchRepository(totalCount: 20),
            refreshRepository,
            providerDelayMs: 500);
    }

    public static LoadTestEnvironment CreateEmptyCatalog(string query, int providerDelayMs = 500)
    {
        return Create(
            query,
            new LoadTestSearchRepository(totalCount: 0),
            new LoadTestRefreshRepository(),
            providerDelayMs,
            providerSucceeds: true);
    }

    public static LoadTestEnvironment CreateFreshEmptyCatalog(string query)
    {
        var refreshRepository = new LoadTestRefreshRepository();
        refreshRepository.Seed(query, SearchContentType.All, 1, DateTime.UtcNow.AddHours(-1));

        return Create(
            query,
            new LoadTestSearchRepository(totalCount: 0, simulatePostIngestionPopulation: false),
            refreshRepository,
            providerDelayMs: 0,
            providerSucceeds: true);
    }

    public static LoadTestEnvironment CreateEmptyCatalogWithFailingProvider(string query)
    {
        return Create(
            query,
            new LoadTestSearchRepository(totalCount: 0, simulatePostIngestionPopulation: false),
            new LoadTestRefreshRepository(),
            providerDelayMs: 500,
            providerSucceeds: false);
    }

    private static LoadTestEnvironment Create(
        string query,
        LoadTestSearchRepository repository,
        LoadTestRefreshRepository refreshRepository,
        int providerDelayMs,
        bool providerSucceeds = true)
    {
        var lockDiagnostics = new SearchRefreshLockDiagnostics();
        var completionRegistry = new LocalSearchRefreshCompletionRegistry();
        var lockService = new SearchRefreshLockService(
            new ServiceCollection().BuildServiceProvider(),
            Options.Create(new MovieApp.Infrastructure.Configuration.RedisOptions { ConnectionString = string.Empty }),
            new LocalSearchRefreshSingleFlightGate(),
            lockDiagnostics,
            NullLogger<SearchRefreshLockService>.Instance);
        var completionSignal = new SearchRefreshCompletionSignal(
            new ServiceCollection().BuildServiceProvider(),
            Options.Create(new MovieApp.Infrastructure.Configuration.RedisOptions { ConnectionString = string.Empty }),
            completionRegistry,
            NullLogger<SearchRefreshCompletionSignal>.Instance);
        var providerIngestionService = new LoadTestProviderIngestionService(providerDelayMs, providerSucceeds);
        var criteria = new SearchCriteria(
            query,
            SearchContentType.All,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var service = new SearchService(
            repository,
            new LoadTestSearchHistoryRepository(),
            refreshRepository,
            new LoadTestCurrentUser(),
            new LoadTestCacheService(),
            providerIngestionService,
            lockService,
            completionSignal,
            Options.Create(new SearchOptions
            {
                CacheDuration = TimeSpan.FromHours(1),
                ProviderRefreshInterval = TimeSpan.FromHours(24),
                ProviderRefreshLockDuration = TimeSpan.FromSeconds(30)
            }));

        return new LoadTestEnvironment(
            service,
            criteria,
            providerIngestionService,
            lockDiagnostics,
            refreshRepository);
    }

    public void OutputScenarioSummary(string scenario, LoadTestMetrics metrics)
    {
        Console.WriteLine(scenario);
        Console.WriteLine($"  totalRequests={metrics.TotalRequests}");
        Console.WriteLine($"  successfulRequests={metrics.SuccessfulRequests}");
        Console.WriteLine($"  failedRequests={metrics.FailedRequests}");
        Console.WriteLine($"  providerIngestionCount={ProviderIngestionCount}");
        Console.WriteLine($"  localLockSuccess={LockDiagnostics.LocalAcquireSuccess}");
        Console.WriteLine($"  localLockContention={LockDiagnostics.LocalAcquireContention}");
        Console.WriteLine($"  avgLatencyMs={metrics.AverageLatencyMs:F2}");
        Console.WriteLine($"  p95LatencyMs={metrics.P95LatencyMs}");
        Console.WriteLine($"  maxLatencyMs={metrics.MaxLatencyMs}");
    }
}

internal sealed class LoadTestProviderIngestionService(int delayMs, bool providerSucceeds = true)
    : IUnifiedSearchProviderIngestionService
{
    private int _ingestCount;

    public int IngestCount => _ingestCount;

    public async Task<UnifiedSearchProviderIngestionResult> IngestAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _ingestCount);

        if (delayMs > 0)
        {
            await Task.Delay(delayMs, cancellationToken);
        }

        return new UnifiedSearchProviderIngestionResult(
            true,
            true,
            true,
            true,
            providerSucceeds,
            providerSucceeds,
            providerSucceeds
                ? new PaginatedResult<SearchItem>(
                    [CreateItem("movie"), CreateItem("tv")],
                    criteria.Page,
                    criteria.PageSize,
                    2,
                    1)
                : null);
    }

    public Task<IReadOnlyList<SearchSuggestion>> GetAutocompleteSuggestionsAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SearchSuggestion>>([]);
}

internal sealed class LoadTestSearchRepository(int totalCount, bool simulatePostIngestionPopulation = true) : ISearchRepository
{
    private int _searchCount;

    public int SearchCount => _searchCount;

    public Task<PaginatedResult<SearchItem>> SearchAsync(
        SearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _searchCount);

        if (simulatePostIngestionPopulation && _searchCount > 1 && totalCount == 0)
        {
            return Task.FromResult(new PaginatedResult<SearchItem>(
                [CreateItem("movie"), CreateItem("tv")],
                criteria.Page,
                criteria.PageSize,
                2,
                1));
        }

        return Task.FromResult(new PaginatedResult<SearchItem>(
            totalCount == 0 ? [] : [CreateItem("movie")],
            criteria.Page,
            criteria.PageSize,
            totalCount,
            Math.Max(1, (int)Math.Ceiling(totalCount / (double)criteria.PageSize))));
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

    public Task<IReadOnlySet<CatalogContentKey>> GetContentKeysWithGenreAsync(
        IReadOnlyList<SearchItem> items,
        Guid genreId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<CatalogContentKey>>(new HashSet<CatalogContentKey>());

    public Task<PaginatedResult<SearchItem>> GetByGenreAsync(
        string genreName,
        DiscoveryCriteria criteria,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PaginatedResult<SearchItem>([], 1, 20, 0, 0));

    private static SearchItem CreateItem(string type) =>
        new(
            Guid.NewGuid(),
            type,
            "Result",
            null,
            "Overview",
            "/poster.jpg",
            null,
            new DateOnly(2020, 1, 1),
            8.0m,
            100,
            2020);
}

internal sealed class LoadTestRefreshRepository : ISearchProviderRefreshRepository
{
    private readonly Dictionary<(string Query, SearchContentType Type, int Page), DateTime> _entries = new();

    public int SetCount { get; private set; }

    public void Seed(string normalizedQuery, SearchContentType type, int page, DateTime refreshedAtUtc) =>
        _entries[(normalizedQuery, type, page)] = refreshedAtUtc;

    public Task<DateTime?> GetLastRefreshedAtUtcAsync(
        string normalizedQuery,
        SearchContentType contentType,
        int page,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_entries.TryGetValue((normalizedQuery, contentType, page), out var value)
            ? value
            : (DateTime?)null);

    public Task SetLastRefreshedAtUtcAsync(
        string normalizedQuery,
        SearchContentType contentType,
        int page,
        DateTime refreshedAtUtc,
        CancellationToken cancellationToken = default)
    {
        SetCount++;
        _entries[(normalizedQuery, contentType, page)] = refreshedAtUtc;
        return Task.CompletedTask;
    }
}

internal sealed class LoadTestCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
        Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        where T : class =>
        Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class LoadTestSearchHistoryRepository : ISearchHistoryRepository
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

internal sealed class LoadTestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    public Guid? UserId => null;
}
