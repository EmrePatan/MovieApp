using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Collections;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Collections;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.Collections;

public sealed class GetCollectionServiceTests
{
    [Fact]
    public async Task GetAsyncReturnsMaterializedCollectionWithCatalogGuids()
    {
        var repository = new TrackingMovieRepository();
        var cache = new InMemoryCacheService();
        var service = CreateService(new FakeCollectionDataProvider(), repository, cache);

        var result = await service.GetAsync(FakeCollectionDataProvider.SpaceOdysseyCollectionId);

        Assert.Equal(FakeCollectionDataProvider.SpaceOdysseyCollectionId, result.TmdbId);
        Assert.Equal("Space Odyssey Collection", result.Name);
        Assert.Equal(3, result.Parts.Count);
        Assert.All(result.Parts, part => Assert.NotEqual(Guid.Empty, part.Id));
        Assert.Equal(FakeMovieDataProvider.InterstellarTmdbId, result.Parts[0].TmdbId);
        Assert.Equal(1, repository.EnsureCallCount);
        Assert.Equal(3, repository.LastSummaryBatchCount);
    }

    [Fact]
    public async Task GetAsyncUsesCacheAfterMaterialization()
    {
        var repository = new TrackingMovieRepository();
        var provider = new CountingCollectionDataProvider();
        var cache = new InMemoryCacheService();
        var service = CreateService(provider, repository, cache);

        await service.GetAsync(FakeCollectionDataProvider.SpaceOdysseyCollectionId);
        await service.GetAsync(FakeCollectionDataProvider.SpaceOdysseyCollectionId);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, repository.EnsureCallCount);
    }

    [Fact]
    public async Task GetAsyncThrowsNotFoundForUnknownCollection()
    {
        var service = CreateService(
            new FakeCollectionDataProvider(),
            new TrackingMovieRepository(),
            new InMemoryCacheService());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetAsync(FakeCollectionDataProvider.UnknownCollectionId));
    }

    [Fact]
    public async Task GetAsyncThrowsValidationForInvalidId()
    {
        var service = CreateService(
            new FakeCollectionDataProvider(),
            new TrackingMovieRepository(),
            new InMemoryCacheService());

        await Assert.ThrowsAsync<ValidationException>(() => service.GetAsync(0));
    }

    [Fact]
    public async Task GetAsyncThrowsWhenProviderFails()
    {
        var provider = new FakeCollectionDataProvider { ShouldThrow = true };
        var service = CreateService(
            provider,
            new TrackingMovieRepository(),
            new InMemoryCacheService());

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(() =>
            service.GetAsync(FakeCollectionDataProvider.SpaceOdysseyCollectionId));
    }

    [Fact]
    public async Task GetAsyncDoesNotCallUpsertFromProviderAsync()
    {
        var repository = new ThrowingUpsertMovieRepository();
        var service = CreateService(
            new FakeCollectionDataProvider(),
            repository,
            new InMemoryCacheService());

        var result = await service.GetAsync(FakeCollectionDataProvider.SpaceOdysseyCollectionId);

        Assert.NotEmpty(result.Parts);
        Assert.Equal(0, repository.UpsertCallCount);
    }

    private static GetCollectionService CreateService(
        ICollectionDataProvider provider,
        IMovieRepository repository,
        ICacheService cache) =>
        new(
            provider,
            repository,
            cache,
            NullLogger<GetCollectionService>.Instance);

    private sealed class CountingCollectionDataProvider : ICollectionDataProvider
    {
        public int CallCount { get; private set; }

        public Task<CollectionProviderDetails?> GetCollectionAsync(
            int tmdbCollectionId,
            CancellationToken cancellationToken = default)
        {
            CallCount += 1;
            return new FakeCollectionDataProvider().GetCollectionAsync(tmdbCollectionId, cancellationToken);
        }
    }

    private sealed class TrackingMovieRepository : IMovieRepository
    {
        public int EnsureCallCount { get; private set; }

        public int LastSummaryBatchCount { get; private set; }

        private readonly Dictionary<int, Guid> _ids = new();

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            EnsureCallCount += 1;
            LastSummaryBatchCount = summaries.Count;

            foreach (var summary in summaries)
            {
                if (!summary.TmdbId.HasValue || _ids.ContainsKey(summary.TmdbId.Value))
                {
                    continue;
                }

                _ids[summary.TmdbId.Value] = Guid.NewGuid();
            }

            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>(_ids));
        }
    }

    private sealed class ThrowingUpsertMovieRepository : IMovieRepository
    {
        public int UpsertCallCount { get; private set; }

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default)
        {
            UpsertCallCount += 1;
            throw new InvalidOperationException("UpsertFromProviderAsync should not be called.");
        }

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid()));
        }
    }

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (_entries.TryGetValue(key, out var value))
            {
                return Task.FromResult((T?)value);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value!;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }
}
