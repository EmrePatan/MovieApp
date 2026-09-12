using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Movies;

public sealed class SearchMoviesServiceTests
{
    [Fact]
    public async Task SearchAsyncContinuesWhenOneResultHasPersistenceConflict()
    {
        var summaries = new[]
        {
            CreateSummary(930001, "fake-tmdb-930001"),
            CreateSummary(930002, "fake-tmdb-930002")
        };

        var service = CreateService(
            new FakeMovieDataProvider(summaries),
            new ConflictOnSecondUpsertMovieRepository(),
            new FakeCacheService(null));

        var result = await service.SearchAsync(new MovieSearchRequest("duplicate-imdb", 1, 20));

        Assert.Single(result.Items);
        Assert.Equal(930001, result.Items[0].TmdbId);
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsyncPropagatesUnrelatedRepositoryExceptions()
    {
        var summaries = new[] { CreateSummary(930001, "fake-tmdb-930001") };

        var service = CreateService(
            new FakeMovieDataProvider(summaries),
            new ThrowingMovieRepository(new InvalidOperationException("database unavailable")),
            new FakeCacheService(null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SearchAsync(new MovieSearchRequest("duplicate-imdb", 1, 20)));
    }

    private static SearchMoviesService CreateService(
        IMovieDataProvider movieDataProvider,
        IMovieRepository movieRepository,
        ICacheService cacheService,
        SearchOptions? searchOptions = null) =>
        new(
            movieDataProvider,
            movieRepository,
            cacheService,
            Options.Create(searchOptions ?? new SearchOptions()),
            NullLogger<SearchMoviesService>.Instance);

    private static MovieProviderSummary CreateSummary(int tmdbId, string externalId) =>
        new(
            externalId,
            tmdbId,
            null,
            "tt9300001",
            $"Movie {tmdbId}",
            "Overview",
            new DateOnly(2020, 1, 1),
            "/poster.jpg",
            7.0m,
            100);

    private sealed class FakeMovieDataProvider(IReadOnlyList<MovieProviderSummary> summaries) : IMovieDataProvider
    {
        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MovieProviderSearchResult(
                summaries,
                page,
                pageSize,
                summaries.Count,
                1));

        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            CancellationToken cancellationToken = default)
        {
            var summary = summaries.SingleOrDefault(item =>
                string.Equals(item.ExternalId, externalId, StringComparison.OrdinalIgnoreCase));

            if (summary is null)
            {
                return Task.FromResult<MovieProviderDetails?>(null);
            }

            return Task.FromResult<MovieProviderDetails?>(new MovieProviderDetails(
                summary.ExternalId,
                summary.TmdbId,
                summary.TvdbId,
                summary.ImdbId,
                summary.Title,
                summary.Title,
                summary.Overview,
                summary.ReleaseDate,
                100,
                summary.PosterPath,
                "/backdrop.jpg",
                "en",
                summary.VoteAverage,
                summary.VoteCount,
                ["Drama"]));
        }
    }

    private sealed class ConflictOnSecondUpsertMovieRepository : IMovieRepository
    {
        private int _upsertCount;

        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default)
        {
            _upsertCount++;

            if (_upsertCount == 2)
            {
                throw new MovieExternalIdPersistenceConflictException(
                    details.TmdbId,
                    details.ExternalId);
            }

            return Task.FromResult(CreateMovie(details));
        }

        private static Movie CreateMovie(MovieProviderDetails details) =>
            new()
            {
                Id = Guid.NewGuid(),
                TmdbId = details.TmdbId,
                ImdbId = details.ImdbId,
                Title = details.Title,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
    }

    private sealed class ThrowingMovieRepository(Exception exception) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class FakeCacheService(MovieSearchCacheEntry? entry) : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(entry?.Result as T);

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
}
