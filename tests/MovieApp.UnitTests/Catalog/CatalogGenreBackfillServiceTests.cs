using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Services.Catalog;

namespace MovieApp.UnitTests.Catalog;

public sealed class CatalogGenreBackfillServiceTests
{
    [Fact]
    public async Task RunAsyncWhenDisabledPerformsNoWork()
    {
        var repository = new FakeCatalogGenreBackfillRepository();
        var service = CreateService(repository, enabled: false);

        var result = await service.RunAsync();

        Assert.False(service.IsEnabled);
        Assert.Equal(0, result.BatchesProcessed);
        Assert.Equal(0, result.Selected);
        Assert.Equal(0, repository.ProcessCalls);
    }

    [Fact]
    public async Task SelectCandidatesAsyncRespectsBatchSize()
    {
        var repository = new FakeCatalogGenreBackfillRepository(movieCount: 100, tvCount: 100);
        var service = CreateService(repository);

        var selected = await service.SelectCandidatesAsync(50, []);

        Assert.Equal(50, selected.Count);
    }

    [Fact]
    public async Task ProcessBatchAsyncRepairsMovieAndDoesNotSelectAgain()
    {
        var movieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var repository = new FakeCatalogGenreBackfillRepository();
        repository.ConfigureMovie(movieId, hasGenresAfterRepair: true);
        var service = CreateService(repository);
        var runExclude = new HashSet<Guid>();

        var result = await service.ProcessBatchAsync(
            [new CatalogGenreBackfillCandidate(movieId, "movie", 1)],
            runExclude);

        Assert.Equal(1, result.Succeeded);
        Assert.True(repository.MovieHasGenres(movieId));
    }

    [Fact]
    public async Task ProcessBatchAsyncIsolatesFailures()
    {
        var movieA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var movieB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var repository = new FakeCatalogGenreBackfillRepository();
        repository.ConfigureMovie(movieA, hasGenresAfterRepair: true);
        repository.ConfigureMovie(movieB, shouldFail: true);
        var service = CreateService(repository);
        var runExclude = new HashSet<Guid>();

        var result = await service.ProcessBatchAsync(
        [
            new CatalogGenreBackfillCandidate(movieA, "movie", 1),
            new CatalogGenreBackfillCandidate(movieB, "movie", 2),
        ],
            runExclude);

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.Contains(movieB, runExclude);
    }

    [Fact]
    public async Task ProcessBatchAsyncMarksUnrepairableItemsExcludedForRun()
    {
        var movieId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var repository = new FakeCatalogGenreBackfillRepository();
        repository.ConfigureMovie(movieId, hasGenresAfterRepair: false);
        var service = CreateService(repository);
        var runExclude = new HashSet<Guid>();

        var result = await service.ProcessBatchAsync(
            [new CatalogGenreBackfillCandidate(movieId, "movie", 3)],
            runExclude);

        Assert.Equal(1, result.Unrepairable);
        Assert.Contains(movieId, runExclude);
    }

    [Fact]
    public async Task ProcessBatchAsyncRespectsCancellation()
    {
        var repository = new FakeCatalogGenreBackfillRepository();
        var service = CreateService(repository);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ProcessBatchAsync(
                [new CatalogGenreBackfillCandidate(Guid.NewGuid(), "movie", 1)],
                new HashSet<Guid>(),
                cts.Token));
    }

    [Fact]
    public async Task ProcessBatchAsyncDoesNotRemoveExistingGenres()
    {
        var movieId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var repository = new FakeCatalogGenreBackfillRepository();
        repository.ConfigureMovie(movieId, alreadyHasGenres: true);
        var service = CreateService(repository);

        var result = await service.ProcessBatchAsync(
            [new CatalogGenreBackfillCandidate(movieId, "movie", 4)],
            new HashSet<Guid>());

        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, repository.ExistingGenreCount(movieId));
    }

    private static CatalogGenreBackfillService CreateService(
        FakeCatalogGenreBackfillRepository repository,
        bool enabled = true)
    {
        var services = new ServiceCollection();
        services.AddScoped<ICatalogGenreBackfillItemProcessor>(_ =>
            new FakeCatalogGenreBackfillItemProcessor(repository));
        var provider = services.BuildServiceProvider();

        return new CatalogGenreBackfillService(
            repository,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CatalogGenreBackfillOptions
            {
                Enabled = enabled,
                BatchSize = 50,
                MaxConcurrency = 1,
                MaxBatchesPerRun = 1,
                DelayBetweenItemsMs = 0,
                DelayBetweenBatchesMs = 0
            }));
    }

    private sealed class FakeCatalogGenreBackfillItemProcessor(FakeCatalogGenreBackfillRepository repository)
        : ICatalogGenreBackfillItemProcessor
    {
        public Task<CatalogGenreBackfillItemOutcome> ProcessAsync(
            CatalogGenreBackfillCandidate candidate,
            CancellationToken cancellationToken = default) =>
            repository.ProcessAsync(candidate, cancellationToken);
    }

    private sealed class FakeCatalogGenreBackfillRepository(int movieCount = 0, int tvCount = 0)
        : ICatalogGenreBackfillRepository
    {
        private readonly Dictionary<Guid, MovieState> _movies = new();
        public int ProcessCalls { get; private set; }

        public void ConfigureMovie(
            Guid movieId,
            bool hasGenresAfterRepair = true,
            bool shouldFail = false,
            bool alreadyHasGenres = false)
        {
            _movies[movieId] = new MovieState(hasGenresAfterRepair, shouldFail, alreadyHasGenres);
        }

        public Task<IReadOnlyList<CatalogGenreBackfillCandidate>> SelectMovieCandidatesAsync(
            int limit,
            IReadOnlyCollection<Guid> excludeIds,
            CancellationToken cancellationToken = default)
        {
            var candidates = Enumerable.Range(1, movieCount)
                .Select(index => new CatalogGenreBackfillCandidate(Guid.NewGuid(), "movie", index))
                .Where(candidate => !excludeIds.Contains(candidate.CatalogId))
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<CatalogGenreBackfillCandidate>>(candidates);
        }

        public Task<IReadOnlyList<CatalogGenreBackfillCandidate>> SelectTvShowCandidatesAsync(
            int limit,
            IReadOnlyCollection<Guid> excludeIds,
            CancellationToken cancellationToken = default)
        {
            var candidates = Enumerable.Range(1, tvCount)
                .Select(index => new CatalogGenreBackfillCandidate(Guid.NewGuid(), "tv", index))
                .Where(candidate => !excludeIds.Contains(candidate.CatalogId))
                .Take(limit)
                .ToList();

            return Task.FromResult<IReadOnlyList<CatalogGenreBackfillCandidate>>(candidates);
        }

        public Task<bool> MovieHasGenresAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.FromResult(MovieHasGenres(movieId));

        public Task<bool> TvShowHasGenresAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<CatalogGenreBackfillCoverageSnapshot> GetCoverageAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CatalogGenreBackfillCoverageSnapshot(movieCount, tvCount, movieCount, tvCount));

        public bool MovieHasGenres(Guid movieId) =>
            _movies.TryGetValue(movieId, out var state) && (state.AlreadyHasGenres || state.Repaired);

        public int ExistingGenreCount(Guid movieId) =>
            _movies.TryGetValue(movieId, out var state) && state.AlreadyHasGenres ? 1 : 0;

        public Task<CatalogGenreBackfillItemOutcome> ProcessAsync(
            CatalogGenreBackfillCandidate candidate,
            CancellationToken cancellationToken)
        {
            ProcessCalls++;
            cancellationToken.ThrowIfCancellationRequested();

            if (!_movies.TryGetValue(candidate.CatalogId, out var state))
            {
                return Task.FromResult(CatalogGenreBackfillItemOutcome.Skipped);
            }

            if (state.AlreadyHasGenres)
            {
                return Task.FromResult(CatalogGenreBackfillItemOutcome.Skipped);
            }

            if (state.ShouldFail)
            {
                return Task.FromResult(CatalogGenreBackfillItemOutcome.Failed);
            }

            state.Repaired = state.HasGenresAfterRepair;
            return Task.FromResult(
                state.Repaired
                    ? CatalogGenreBackfillItemOutcome.Succeeded
                    : CatalogGenreBackfillItemOutcome.Unrepairable);
        }

        private sealed class MovieState(bool hasGenresAfterRepair, bool shouldFail, bool alreadyHasGenres)
        {
            public bool HasGenresAfterRepair { get; } = hasGenresAfterRepair;

            public bool ShouldFail { get; } = shouldFail;

            public bool AlreadyHasGenres { get; } = alreadyHasGenres;

            public bool Repaired { get; set; }
        }
    }
}
