using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.People;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.People;

public sealed class PersonFilmographyComposerTests
{
    [Fact]
    public async Task DeduplicatesSortsAndResolvesExistingCatalogIdsOnly()
    {
        var movieRepository = new FakeMovieRepository();
        var tvShowRepository = new FakeTvShowRepository();

        var credits = new List<PersonFilmographyCredit>
        {
            new("movie", 10, "Older Film", "/old.jpg", "Hero", new DateOnly(2010, 1, 1)),
            new("movie", 10, "Older Film", "/old.jpg", "Hero", new DateOnly(2010, 1, 1)),
            new("tv", 20, "Recent Show", "/recent.jpg", "Lead", new DateOnly(2022, 5, 1)),
            new("tv", 30, "Undated Show", null, "Guest", null),
            new("movie", 99, "Missing Movie", null, "Nobody", new DateOnly(2020, 1, 1)),
            new("movie", 0, "Broken", null, "Nobody", null)
        };

        var result = await PersonFilmographyComposer.ComposeAsync(
            credits,
            movieRepository,
            tvShowRepository,
            CancellationToken.None);

        Assert.Equal(4, result.Count);
        Assert.Equal("tv", result[0].MediaType);
        Assert.Equal(20, result[0].TmdbId);
        Assert.Equal(tvShowRepository.TvIds[20], result[0].CatalogId);
        Assert.Equal("Missing Movie", result[1].Title);
        Assert.Equal(99, result[1].TmdbId);
        Assert.Null(result[1].CatalogId);
        Assert.Equal("movie", result[2].MediaType);
        Assert.Equal(10, result[2].TmdbId);
        Assert.Equal(movieRepository.MovieIds[10], result[2].CatalogId);
        Assert.Equal("Undated Show", result[3].Title);
        Assert.Null(result[3].ReleaseDate);
        Assert.False(movieRepository.EnsureFromSummariesCalled);
        Assert.False(tvShowRepository.EnsureFromSummariesCalled);
    }

    private sealed class FakeMovieRepository : IMovieRepository
    {
        public Dictionary<int, Guid> MovieIds { get; } = new()
        {
            [10] = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
        };

        public bool EnsureFromSummariesCalled { get; private set; }

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                MovieIds.Where(pair => tmdbIds.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value));

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            EnsureFromSummariesCalled = true;
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(MovieIds);
        }

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTvShowRepository : ITvShowRepository
    {
        public Dictionary<int, Guid> TvIds { get; } = new()
        {
            [20] = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            [30] = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")
        };

        public bool EnsureFromSummariesCalled { get; private set; }

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                TvIds.Where(pair => tmdbIds.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value));

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            EnsureFromSummariesCalled = true;
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(TvIds);
        }

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
