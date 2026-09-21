using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.People;
using MovieApp.Domain.Entities;
using MovieApp.Infrastructure.Providers;

namespace MovieApp.UnitTests.People;

public sealed class GetPersonByTmdbIdServiceTests
{
    [Fact]
    public async Task GetAsyncReturnsPersonDetailWithFilmography()
    {
        var personRepository = new FakePersonRepository();
        var service = new GetPersonByTmdbIdService(
            new FakePersonDataProvider(),
            personRepository,
            new NoOpMovieRepository(),
            new NoOpTvShowRepository(),
            new NoOpCacheService());

        var result = await service.GetAsync(FakePersonDataProvider.McConaugheyTmdbId);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(FakePersonDataProvider.McConaugheyTmdbId, result.TmdbId);
        Assert.Equal("Matthew McConaughey", result.Name);
        Assert.Equal(2, result.Filmography.Count);
        Assert.Equal("Interstellar", result.Filmography[0].Title);
        Assert.Equal("Untitled Series", result.Filmography[1].Title);
    }

    [Fact]
    public async Task GetAsyncThrowsNotFoundForUnknownPerson()
    {
        var service = new GetPersonByTmdbIdService(
            new FakePersonDataProvider(),
            new FakePersonRepository(),
            new NoOpMovieRepository(),
            new NoOpTvShowRepository(),
            new NoOpCacheService());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(999999));
    }

    [Fact]
    public async Task GetAsyncSkipsUpsertWhenNameAndProfilePathAreUnchanged()
    {
        var repository = new FakePersonRepository
        {
            ExistingPerson = new Person
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                TmdbId = FakePersonDataProvider.McConaugheyTmdbId,
                Name = "Matthew McConaughey",
                ProfilePath = "/fake/cooper.jpg",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        var service = new GetPersonByTmdbIdService(
            new FakePersonDataProvider(),
            repository,
            new NoOpMovieRepository(),
            new NoOpTvShowRepository(),
            new NoOpCacheService());

        var result = await service.GetAsync(FakePersonDataProvider.McConaugheyTmdbId);

        Assert.Equal(0, repository.UpsertCallCount);
        Assert.Equal(repository.ExistingPerson.Id, result.Id);
        Assert.Equal(2, result.Filmography.Count);
    }

    [Fact]
    public async Task GetAsyncUpsertsWhenNameChanges()
    {
        var repository = new FakePersonRepository
        {
            ExistingPerson = new Person
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                TmdbId = FakePersonDataProvider.McConaugheyTmdbId,
                Name = "Old Name",
                ProfilePath = "/fake/cooper.jpg",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        var service = new GetPersonByTmdbIdService(
            new FakePersonDataProvider(),
            repository,
            new NoOpMovieRepository(),
            new NoOpTvShowRepository(),
            new NoOpCacheService());

        await service.GetAsync(FakePersonDataProvider.McConaugheyTmdbId);

        Assert.Equal(1, repository.UpsertCallCount);
    }

    [Fact]
    public async Task GetAsyncUpsertsWhenProfilePathChanges()
    {
        var repository = new FakePersonRepository
        {
            ExistingPerson = new Person
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                TmdbId = FakePersonDataProvider.McConaugheyTmdbId,
                Name = "Matthew McConaughey",
                ProfilePath = "/old-profile.jpg",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };
        var service = new GetPersonByTmdbIdService(
            new FakePersonDataProvider(),
            repository,
            new NoOpMovieRepository(),
            new NoOpTvShowRepository(),
            new NoOpCacheService());

        await service.GetAsync(FakePersonDataProvider.McConaugheyTmdbId);

        Assert.Equal(1, repository.UpsertCallCount);
    }

    [Fact]
    public async Task GetAsync_ReturnsCachedResult_OnSecondRequest()
    {
        var repository = new CountingPersonRepository();
        var service = new GetPersonByTmdbIdService(
            new FakePersonDataProvider(),
            repository,
            new NoOpMovieRepository(),
            new NoOpTvShowRepository(),
            new InMemoryCacheService());

        await service.GetAsync(FakePersonDataProvider.McConaugheyTmdbId);
        await service.GetAsync(FakePersonDataProvider.McConaugheyTmdbId);

        Assert.Equal(1, repository.GetByTmdbIdCallCount);
    }

    private sealed class CountingPersonRepository : IPersonRepository
    {
        public int GetByTmdbIdCallCount { get; private set; }

        public Task<Person?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default)
        {
            GetByTmdbIdCallCount++;
            return Task.FromResult<Person?>(null);
        }

        public Task<Person> UpsertFromProviderAsync(
            int tmdbId,
            string name,
            string? profilePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Person
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                TmdbId = tmdbId,
                Name = name,
                ProfilePath = profilePath,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<PersonProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (_entries.TryGetValue(key, out var value) && value is T typed)
            {
                return Task.FromResult<T?>(typed);
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

    private sealed class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakePersonRepository : IPersonRepository
    {
        public Person? ExistingPerson { get; init; }

        public int UpsertCallCount { get; private set; }

        public Task<Person?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingPerson);

        public Task<Person> UpsertFromProviderAsync(
            int tmdbId,
            string name,
            string? profilePath,
            CancellationToken cancellationToken = default)
        {
            UpsertCallCount++;
            return Task.FromResult(new Person
            {
                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                TmdbId = tmdbId,
                Name = name,
                ProfilePath = profilePath,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<PersonProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries.ToDictionary(summary => summary.TmdbId, _ => Guid.NewGuid()));
    }

    private sealed class NoOpMovieRepository : IMovieRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpTvShowRepository : ITvShowRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
