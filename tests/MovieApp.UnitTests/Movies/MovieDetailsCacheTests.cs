using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;
using MovieApp.UnitTests.Keywords;

namespace MovieApp.UnitTests.Movies;

public sealed class MovieDetailsCacheTests
{
    private static readonly Guid MovieId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task GetByIdAsyncUsesCacheAndSkipsRepositoryOnSecondCall()
    {
        var repository = new CountingMovieRepository(CreateMovie());
        var cache = new InMemoryCacheService();
        var service = CreateService(repository, cache);

        await service.GetByIdAsync(MovieId);
        await service.GetByIdAsync(MovieId);

        Assert.Equal(1, repository.GetByIdCallCount);
    }

    [Fact]
    public async Task GetByIdAsyncWithPrefetchedMovieSkipsRepositoryLoad()
    {
        var repository = new CountingMovieRepository(CreateMovie());
        var service = CreateService(repository, new InMemoryCacheService());

        await service.GetByIdAsync(MovieId, CreateMovie());

        Assert.Equal(0, repository.GetByIdCallCount);
    }

    private static GetMovieByIdService CreateService(CountingMovieRepository repository, ICacheService cache) =>
        new(
            repository,
            new NoOpMovieRegionalReleaseRepository(),
            Options.Create(new ReleaseRegionOptions { DefaultRegion = "TR" }),
            new NoOpCatalogKeywordReadPathScheduler(),
            new NullMovieDataProvider(),
            new NoOpCatalogProviderUpsertService(),
            cache);

    private static Movie CreateMovie() =>
        new()
        {
            Id = MovieId,
            Title = "Cached Movie",
            ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private sealed class CountingMovieRepository(Movie movie) : IMovieRepository
    {
        public int GetByIdCallCount { get; private set; }

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount += 1;
            return Task.FromResult<Movie?>(movie);
        }

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpMovieRegionalReleaseRepository : IMovieRegionalReleaseRepository
    {
        public Task<MovieRegionalRelease?> GetByMovieIdAndRegionAsync(
            Guid movieId,
            string region,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieRegionalRelease?>(null);

        public Task<MovieRegionalRelease> UpsertAsync(
            MovieRegionalRelease regionalRelease,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(regionalRelease);
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

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }
}
