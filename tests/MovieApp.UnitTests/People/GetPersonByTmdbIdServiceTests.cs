using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
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
            new NoOpTvShowRepository());

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
            new NoOpTvShowRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(999999));
    }

    private sealed class FakePersonRepository : IPersonRepository
    {
        public Task<Person?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Person?>(null);

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
