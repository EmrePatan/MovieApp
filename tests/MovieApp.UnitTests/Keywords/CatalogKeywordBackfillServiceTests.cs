using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.UnitTests.Keywords;

public sealed class CatalogKeywordBackfillServiceTests
{
    [Fact]
    public async Task SelectCandidatesAsyncRespectsBatchSize()
    {
        var repository = CreateRepository(movieCount: 100, tvCount: 100);
        var service = CreateService(repository);

        var selected = await service.SelectCandidatesAsync(25);

        Assert.Equal(25, selected.Count);
    }

    [Fact]
    public async Task SelectCandidatesAsyncAllocatesBothContentTypes()
    {
        var repository = CreateRepository(movieCount: 100, tvCount: 100);
        var service = CreateService(repository);

        var selected = await service.SelectCandidatesAsync(20);

        Assert.Equal(10, selected.Count(candidate => candidate.ContentType == "movie"));
        Assert.Equal(10, selected.Count(candidate => candidate.ContentType == "tv"));
    }

    [Fact]
    public async Task SelectCandidatesAsyncFillsUnusedMovieCapacityWithTvShows()
    {
        var repository = CreateRepository(movieCount: 2, tvCount: 100);
        var service = CreateService(repository);

        var selected = await service.SelectCandidatesAsync(20);

        Assert.Equal(2, selected.Count(candidate => candidate.ContentType == "movie"));
        Assert.Equal(18, selected.Count(candidate => candidate.ContentType == "tv"));
    }

    [Fact]
    public async Task ProcessBatchAsyncIsolatesFailures()
    {
        var movieA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var movieB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var movieC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var repository = CreateRepository();
        var ingestion = new TrackingKeywordIngestionService(repository)
        {
            FailMovieIds = [movieB]
        };
        var service = CreateService(repository, ingestion);

        var result = await service.ProcessBatchAsync(
        [
            new CatalogKeywordBackfillCandidate(movieA, "movie", 1),
            new CatalogKeywordBackfillCandidate(movieB, "movie", 2),
            new CatalogKeywordBackfillCandidate(movieC, "movie", 3),
        ]);

        Assert.Equal(3, result.Selected);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.Contains(movieA, repository.SyncedMovieIds);
        Assert.DoesNotContain(movieB, repository.SyncedMovieIds);
        Assert.Contains(movieC, repository.SyncedMovieIds);
    }

    [Fact]
    public async Task ProcessBatchAsyncSkipsAlreadySyncedTitles()
    {
        var movieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var repository = CreateRepository();
        repository.PreSyncedMovieIds.Add(movieId);
        var ingestion = new TrackingKeywordIngestionService(repository);
        var service = CreateService(repository, ingestion);

        var result = await service.ProcessBatchAsync(
            [new CatalogKeywordBackfillCandidate(movieId, "movie", 1)]);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, ingestion.MovieCalls);
    }

    [Fact]
    public async Task ProcessBatchAsyncLimitsActiveProviderConcurrency()
    {
        var repository = CreateRepository();
        var ingestion = new ConcurrencyTrackingKeywordIngestionService();
        var service = CreateService(repository, ingestion, maxConcurrency: 2);
        var candidates = Enumerable.Range(0, 8)
            .Select(index => new CatalogKeywordBackfillCandidate(
                Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{index:D012}"),
                "movie",
                index + 1))
            .ToList();

        await service.ProcessBatchAsync(candidates);

        Assert.True(ingestion.MaxObservedConcurrency <= 2);
        Assert.Equal(8, ingestion.MovieCalls);
    }

    [Fact]
    public async Task ProcessBatchAsyncUsesIndependentScopePerConcurrentWorker()
    {
        var tracker = new ConcurrentScopeTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddSingleton<FakeBackfillRepository>();
        services.AddSingleton<ICatalogKeywordBackfillRepository>(provider =>
            provider.GetRequiredService<FakeBackfillRepository>());
        services.AddScoped<ICatalogKeywordIngestionService, ScopeTrackingKeywordIngestionService>();
        services.AddScoped<ICatalogKeywordBackfillItemProcessor, CatalogKeywordBackfillItemProcessor>();
        services.AddScoped<ICatalogKeywordBackfillService, CatalogKeywordBackfillService>();
        services.AddOptions<CatalogKeywordBackfillOptions>().Configure(options =>
        {
            options.BatchSize = 25;
            options.MaxConcurrency = 2;
        });

        await using var provider = services.BuildServiceProvider();
        await using var outerScope = provider.CreateAsyncScope();
        var service = outerScope.ServiceProvider.GetRequiredService<ICatalogKeywordBackfillService>();
        var candidates = Enumerable.Range(0, 8)
            .Select(index => new CatalogKeywordBackfillCandidate(
                Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{index:D012}"),
                "movie",
                index + 1))
            .ToList();

        await service.ProcessBatchAsync(candidates);

        Assert.True(tracker.MaxConcurrentDistinctScopedInstances >= 2);
    }

    private static CatalogKeywordBackfillService CreateService(
        FakeBackfillRepository repository,
        ICatalogKeywordIngestionService? ingestion = null,
        int batchSize = 25,
        int maxConcurrency = 2)
    {
        var processor = new CatalogKeywordBackfillItemProcessor(
            repository,
            ingestion ?? new TrackingKeywordIngestionService(repository));
        var scopeFactory = new SingleProcessorScopeFactory(processor);

        return new CatalogKeywordBackfillService(
            repository,
            scopeFactory,
            Options.Create(new CatalogKeywordBackfillOptions
            {
                BatchSize = batchSize,
                MaxConcurrency = maxConcurrency
            }));
    }

    private static FakeBackfillRepository CreateRepository(int movieCount = 0, int tvCount = 0)
    {
        var repository = new FakeBackfillRepository();
        repository.MovieCandidates.AddRange(
            Enumerable.Range(0, movieCount)
                .Select(index => Guid.Parse($"aaaaaaaa-aaaa-aaaa-aaaa-{index:D012}")));
        repository.TvShowCandidates.AddRange(
            Enumerable.Range(0, tvCount)
                .Select(index => Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-{index:D012}")));
        return repository;
    }

    private sealed class SingleProcessorScopeFactory(ICatalogKeywordBackfillItemProcessor processor) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new SingleProcessorScope(processor);
    }

    private sealed class SingleProcessorScope(ICatalogKeywordBackfillItemProcessor processor) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new SingleProcessorServiceProvider(processor);

        public void Dispose()
        {
        }
    }

    private sealed class SingleProcessorServiceProvider(ICatalogKeywordBackfillItemProcessor processor) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ICatalogKeywordBackfillItemProcessor) ? processor : null;
    }

    private sealed class FakeBackfillRepository : ICatalogKeywordBackfillRepository
    {
        public List<Guid> MovieCandidates { get; } = [];

        public List<Guid> TvShowCandidates { get; } = [];

        public HashSet<Guid> PreSyncedMovieIds { get; } = [];

        public HashSet<Guid> PreSyncedTvShowIds { get; } = [];

        public HashSet<Guid> SyncedMovieIds { get; } = [];

        public HashSet<Guid> SyncedTvShowIds { get; } = [];

        public Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectMovieCandidatesAsync(
            int limit,
            IReadOnlyCollection<Guid> excludeIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogKeywordBackfillCandidate>>(
                MovieCandidates
                    .Where(id => !excludeIds.Contains(id))
                    .Take(limit)
                    .Select(id => new CatalogKeywordBackfillCandidate(id, "movie", 1))
                    .ToList());

        public Task<IReadOnlyList<CatalogKeywordBackfillCandidate>> SelectTvShowCandidatesAsync(
            int limit,
            IReadOnlyCollection<Guid> excludeIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogKeywordBackfillCandidate>>(
                TvShowCandidates
                    .Where(id => !excludeIds.Contains(id))
                    .Take(limit)
                    .Select(id => new CatalogKeywordBackfillCandidate(id, "tv", 1))
                    .ToList());

        public Task<CatalogKeywordCoverageSnapshot> GetCoverageAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogKeywordCoverageSnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        public Task<bool> IsMovieKeywordSyncedAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(PreSyncedMovieIds.Contains(movieId) || SyncedMovieIds.Contains(movieId));

        public Task<bool> IsTvShowKeywordSyncedAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(PreSyncedTvShowIds.Contains(tvShowId) || SyncedTvShowIds.Contains(tvShowId));
    }

    private sealed class TrackingKeywordIngestionService(FakeBackfillRepository repository) : ICatalogKeywordIngestionService
    {
        public HashSet<Guid> FailMovieIds { get; init; } = [];

        public int MovieCalls { get; private set; }

        public Task TryEnrichMovieKeywordsAsync(Guid movieId, bool refreshKeywords, IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null, CancellationToken cancellationToken = default)
        {
            MovieCalls++;
            if (!FailMovieIds.Contains(movieId))
            {
                repository.SyncedMovieIds.Add(movieId);
            }

            return Task.CompletedTask;
        }

        public Task TryEnrichTvShowKeywordsAsync(Guid tvShowId, bool refreshKeywords, IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null, CancellationToken cancellationToken = default)
        {
            if (!FailMovieIds.Contains(tvShowId))
            {
                repository.SyncedTvShowIds.Add(tvShowId);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ConcurrencyTrackingKeywordIngestionService : ICatalogKeywordIngestionService
    {
        private int _active;
        private readonly object _lock = new();

        public int MaxObservedConcurrency { get; private set; }

        public int MovieCalls { get; private set; }

        public async Task TryEnrichMovieKeywordsAsync(Guid movieId, bool refreshKeywords, IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null, CancellationToken cancellationToken = default)
        {
            MovieCalls++;
            var active = Interlocked.Increment(ref _active);
            lock (_lock)
            {
                MaxObservedConcurrency = Math.Max(MaxObservedConcurrency, active);
            }

            await Task.Delay(25, cancellationToken);

            Interlocked.Decrement(ref _active);
        }

        public Task TryEnrichTvShowKeywordsAsync(Guid tvShowId, bool refreshKeywords, IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ScopeTrackingKeywordIngestionService(
        ConcurrentScopeTracker tracker,
        FakeBackfillRepository repository) : ICatalogKeywordIngestionService
    {
        public async Task TryEnrichMovieKeywordsAsync(Guid movieId, bool refreshKeywords, IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null, CancellationToken cancellationToken = default)
        {
            await tracker.TrackAsync(this, cancellationToken);
            repository.SyncedMovieIds.Add(movieId);
        }

        public Task TryEnrichTvShowKeywordsAsync(Guid tvShowId, bool refreshKeywords, IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class ConcurrentScopeTracker
    {
        private readonly ConcurrentDictionary<int, byte> _activeScopedInstances = new();

        public int MaxConcurrentDistinctScopedInstances { get; private set; }

        public async Task TrackAsync(object scopedInstance, CancellationToken cancellationToken)
        {
            var scopedInstanceId = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(scopedInstance);
            _activeScopedInstances[scopedInstanceId] = 0;
            UpdateMaxConcurrentDistinctScopedInstances();

            try
            {
                await Task.Delay(50, cancellationToken);
            }
            finally
            {
                _activeScopedInstances.TryRemove(scopedInstanceId, out _);
            }
        }

        private void UpdateMaxConcurrentDistinctScopedInstances()
        {
            var activeCount = _activeScopedInstances.Count;
            if (activeCount > MaxConcurrentDistinctScopedInstances)
            {
                MaxConcurrentDistinctScopedInstances = activeCount;
            }
        }
    }
}
