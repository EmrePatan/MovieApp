using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Services.Keywords;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Keywords;

public sealed class MdbListKeywordBackfillBatchTests
{
    [Fact]
    public async Task HundredMoviesAndHundredTvShowsUseTwoBatchRequests()
    {
        var transport = new RecordingMdbListKeywordTransportProvider();
        var service = CreateCountingService(transport);
        var candidates = BuildCandidates(movies: 100, tvShows: 100);

        var result = await service.ProcessBatchAsync(candidates);

        Assert.Equal(200, result.Selected);
        Assert.Equal(200, result.Succeeded);
        Assert.Equal(2, transport.BatchCalls.Count);
        Assert.Equal(CatalogContentType.Movie, transport.BatchCalls[0].MediaType);
        Assert.Equal(100, transport.BatchCalls[0].TmdbIds.Count);
        Assert.Equal(CatalogContentType.Tv, transport.BatchCalls[1].MediaType);
        Assert.Equal(100, transport.BatchCalls[1].TmdbIds.Count);
        Assert.Equal(0, transport.SingleFetchCount);
    }

    [Fact]
    public async Task TwoHundredMoviesUseSingleBatchRequest()
    {
        var transport = new RecordingMdbListKeywordTransportProvider();
        var service = CreateCountingService(transport);
        var candidates = BuildCandidates(movies: 200, tvShows: 0);

        await service.ProcessBatchAsync(candidates);

        Assert.Single(transport.BatchCalls);
        Assert.Equal(CatalogContentType.Movie, transport.BatchCalls[0].MediaType);
        Assert.Equal(200, transport.BatchCalls[0].TmdbIds.Count);
    }

    [Fact]
    public async Task TwoHundredOneMoviesUseTwoBatchRequests()
    {
        var transport = new RecordingMdbListKeywordTransportProvider();
        var service = CreateCountingService(transport);
        var candidates = BuildCandidates(movies: 201, tvShows: 0);

        await service.ProcessBatchAsync(candidates);

        Assert.Equal(2, transport.BatchCalls.Count);
        Assert.Equal(200, transport.BatchCalls[0].TmdbIds.Count);
        Assert.Single(transport.BatchCalls[1].TmdbIds);
    }

    [Fact]
    public async Task BatchTransportFailureDoesNotAdvanceMarkers()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context, tmdbId: 42);
        var transport = new RecordingMdbListKeywordTransportProvider { ReturnNullBatch = true };
        var service = CreatePersistingService(context, transport);

        var result = await service.ProcessBatchAsync(
        [
            new CatalogKeywordBackfillCandidate(movie.Id, "movie", movie.TmdbId!.Value),
        ]);

        Assert.Equal(1, result.Failed);
        Assert.Null((await context.Movies.FindAsync(movie.Id))!.MdbListKeywordsSyncedAtUtc);
    }

    [Fact]
    public async Task MissingBatchItemIsTreatedAsEmptyKeywordsAndAdvancesMarker()
    {
        await using var context = CreateContext();
        var movie = await SeedMovieAsync(context, tmdbId: 99);
        var transport = new RecordingMdbListKeywordTransportProvider();
        transport.ExcludeTmdbIdsFromBatch.Add(99);
        var service = CreatePersistingService(context, transport);

        var result = await service.ProcessBatchAsync(
        [
            new CatalogKeywordBackfillCandidate(movie.Id, "movie", 99),
        ]);

        Assert.Equal(1, result.Succeeded);
        Assert.NotNull((await context.Movies.FindAsync(movie.Id))!.MdbListKeywordsSyncedAtUtc);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    public void ChunkHelperNeverExceedsTwoHundred(int count)
    {
        var items = Enumerable.Range(1, count).ToList();
        var chunks = MdbListKeywordBatchChunks.Chunk(items, MdbListKeywordBatchChunks.MaxTmdbIdsPerRequest).ToList();

        Assert.All(chunks, chunk => Assert.InRange(chunk.Count, 1, 200));
        Assert.Equal(count, chunks.Sum(chunk => chunk.Count));
        if (count <= 200)
        {
            Assert.Single(chunks);
        }
        else
        {
            Assert.Equal(2, chunks.Count);
        }
    }

    private static MdbListKeywordBackfillService CreateCountingService(RecordingMdbListKeywordTransportProvider transport) =>
        new(
            new MdbListKeywordBackfillRepository(CreateContext()),
            transport,
            new SuccessIngestionScopeFactory(),
            Options.Create(new MdbListKeywordBackfillOptions { BatchSize = 500, MaxConcurrency = 4 }),
            NullLogger<MdbListKeywordBackfillService>.Instance);

    private static MdbListKeywordBackfillService CreatePersistingService(
        ApplicationDbContext context,
        RecordingMdbListKeywordTransportProvider transport) =>
        new(
            new MdbListKeywordBackfillRepository(context),
            transport,
            new PersistingIngestionScopeFactory(context),
            Options.Create(new MdbListKeywordBackfillOptions { BatchSize = 500, MaxConcurrency = 1 }),
            NullLogger<MdbListKeywordBackfillService>.Instance);

    private static List<CatalogKeywordBackfillCandidate> BuildCandidates(int movies, int tvShows)
    {
        var candidates = new List<CatalogKeywordBackfillCandidate>(movies + tvShows);
        for (var index = 0; index < movies; index++)
        {
            candidates.Add(new CatalogKeywordBackfillCandidate(Guid.NewGuid(), "movie", 1_000 + index));
        }

        for (var index = 0; index < tvShows; index++)
        {
            candidates.Add(new CatalogKeywordBackfillCandidate(Guid.NewGuid(), "tv", 2_000 + index));
        }

        return candidates;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"mdblist-backfill-batch-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<Movie> SeedMovieAsync(ApplicationDbContext context, int tmdbId)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            TmdbId = tmdbId,
            Title = "Batch Movie",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        context.Movies.Add(movie);
        await context.SaveChangesAsync();
        return movie;
    }

    private sealed class RecordingMdbListKeywordTransportProvider : IMdbListKeywordTransportProvider
    {
        public bool ReturnNullBatch { get; set; }

        public HashSet<int> ExcludeTmdbIdsFromBatch { get; } = [];

        public int SingleFetchCount { get; private set; }

        public List<(CatalogContentType MediaType, IReadOnlyList<int> TmdbIds)> BatchCalls { get; } = [];

        public Task<MdbListKeywordsTransportResult?> FetchKeywordsAsync(
            CatalogContentType mediaType,
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            SingleFetchCount++;
            return Task.FromResult<MdbListKeywordsTransportResult?>(
                new MdbListKeywordsTransportResult(mediaType, tmdbId, [], IsNotFound: false));
        }

        public Task<MdbListKeywordsBatchTransportResult?> FetchKeywordsBatchAsync(
            CatalogContentType mediaType,
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default)
        {
            BatchCalls.Add((mediaType, tmdbIds.ToList()));
            if (ReturnNullBatch)
            {
                return Task.FromResult<MdbListKeywordsBatchTransportResult?>(null);
            }

            var keywordsByTmdbId = tmdbIds
                .Where(id => !ExcludeTmdbIdsFromBatch.Contains(id))
                .ToDictionary<int, int, IReadOnlyList<MdbListKeywordTransportItem>>(
                    id => id,
                    _ => []);

            return Task.FromResult<MdbListKeywordsBatchTransportResult?>(
                new MdbListKeywordsBatchTransportResult(
                    mediaType,
                    keywordsByTmdbId,
                    new MdbListKeywordTransportTelemetry(200, 1, 10, null)));
        }
    }

    private sealed class SuccessIngestionScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new Scope();

        private sealed class Scope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new Provider();

            public void Dispose()
            {
            }
        }

        private sealed class Provider : IServiceProvider
        {
            public object? GetService(Type serviceType) =>
                serviceType == typeof(IMdbListKeywordIngestionService)
                    ? new StubIngestionService()
                    : null;
        }

        private sealed class StubIngestionService : IMdbListKeywordIngestionService
        {
            public Task<MdbListKeywordIngestionResult> IngestMovieWithProviderKeywordsAsync(
                Guid movieId,
                IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.Succeeded));

            public Task<MdbListKeywordIngestionResult> IngestTvShowWithProviderKeywordsAsync(
                Guid tvShowId,
                IReadOnlyList<MdbListKeywordTransportItem> providerKeywords,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(new MdbListKeywordIngestionResult(MdbListKeywordIngestionStatus.Succeeded));

            public Task<MdbListKeywordIngestionResult> IngestMovieAsync(Guid movieId, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<MdbListKeywordIngestionResult> IngestTvShowAsync(Guid tvShowId, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<MdbListKeywordIngestionResult> IngestMovieByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task<MdbListKeywordIngestionResult> IngestTvShowByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }
    }

    private sealed class PersistingIngestionScopeFactory(ApplicationDbContext context) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new Scope(context);

        private sealed class Scope(ApplicationDbContext context) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new Provider(context);

            public void Dispose()
            {
            }
        }

        private sealed class Provider(ApplicationDbContext context) : IServiceProvider
        {
            public object? GetService(Type serviceType) =>
                serviceType == typeof(IMdbListKeywordIngestionService)
                    ? new MdbListKeywordIngestionService(
                        new RecordingMdbListKeywordTransportProvider(),
                        KeywordCatalogRepositoryTestHelper.CreateRepository(context),
                        NullLogger<MdbListKeywordIngestionService>.Instance)
                    : null;
        }
    }
}
