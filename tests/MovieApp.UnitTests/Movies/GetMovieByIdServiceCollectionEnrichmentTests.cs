using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;
using MovieApp.UnitTests.Keywords;

namespace MovieApp.UnitTests.Movies;

public sealed class GetMovieByIdServiceCollectionEnrichmentTests
{
    private static readonly Guid MovieId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task GetByIdAsync_WhenCollectionMissing_FetchesProviderDetailsAndUpserts()
    {
        var movie = CreateMovie(tmdbCollectionId: null);
        var providerDetails = CreateProviderDetails(tmdbCollectionId: 645, collectionName: "Harry Potter Collection");
        var upsertService = new RecordingCatalogProviderUpsertService(details =>
        {
            movie.TmdbCollectionId = details.TmdbCollectionId;
            movie.CollectionName = details.CollectionName;
            movie.CollectionPosterPath = details.CollectionPosterPath;
            movie.CollectionBackdropPath = details.CollectionBackdropPath;
            return movie;
        });

        var service = CreateService(
            movie,
            new StubMovieDataProvider(providerDetails),
            upsertService);

        var result = await service.GetByIdAsync(MovieId);

        Assert.NotNull(upsertService.LastDetails);
        Assert.Equal(645, upsertService.LastDetails!.TmdbCollectionId);
        Assert.NotNull(result.Collection);
        Assert.Equal(645, result.Collection!.TmdbId);
        Assert.Equal("Harry Potter Collection", result.Collection.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WhenCollectionAlreadyPresent_SkipsProviderFetch()
    {
        var movie = CreateMovie(tmdbCollectionId: 645, collectionName: "Harry Potter Collection");
        var provider = new StubMovieDataProvider(
            CreateProviderDetails(tmdbCollectionId: 999, collectionName: "Other Collection"));

        var service = CreateService(movie, provider, new NoOpCatalogProviderUpsertService());

        var result = await service.GetByIdAsync(MovieId);

        Assert.NotNull(result.Collection);
        Assert.Equal(645, result.Collection!.TmdbId);
        Assert.Equal("Harry Potter Collection", result.Collection.Name);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMovieHasNoCollection_SkipsProviderOnLaterDetailMiss()
    {
        var movie = CreateMovie(tmdbCollectionId: null);
        var provider = new StubMovieDataProvider(
            CreateProviderDetails(tmdbCollectionId: 1, collectionName: "unused") with
            {
                TmdbCollectionId = null,
                CollectionName = null,
                CollectionPosterPath = null,
                CollectionBackdropPath = null
            });
        var cache = new DictionaryCacheService();
        var service = CreateService(movie, provider, new NoOpCatalogProviderUpsertService(), cache);

        await service.GetByIdAsync(MovieId);
        await cache.RemoveAsync(MovieDetailsCacheKeys.Create(MovieId));
        await service.GetByIdAsync(MovieId);

        Assert.Equal(1, provider.GetMovieCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WhenKeywordsAreUnsynced_SchedulesBackgroundEnrichment()
    {
        var movie = CreateMovie(tmdbCollectionId: 645, collectionName: "Harry Potter Collection");
        var scheduler = new RecordingKeywordScheduler();
        var service = CreateService(
            movie,
            new StubMovieDataProvider(null),
            new NoOpCatalogProviderUpsertService(),
            scheduler: scheduler);

        await service.GetByIdAsync(MovieId);

        Assert.Equal([MovieId], scheduler.MovieIds);
    }

    [Fact]
    public async Task GetByIdAsync_WhenKeywordsAreSynced_DoesNotScheduleEnrichment()
    {
        var movie = CreateMovie(tmdbCollectionId: 645, collectionName: "Harry Potter Collection");
        movie.KeywordsSyncedAtUtc = DateTime.UtcNow;
        var scheduler = new RecordingKeywordScheduler();
        var service = CreateService(
            movie,
            new StubMovieDataProvider(null),
            new NoOpCatalogProviderUpsertService(),
            scheduler: scheduler);

        await service.GetByIdAsync(MovieId);

        Assert.Empty(scheduler.MovieIds);
    }

    private static GetMovieByIdService CreateService(
        Movie movie,
        StubMovieDataProvider movieDataProvider,
        ICatalogProviderUpsertService catalogProviderUpsertService,
        ICacheService? cacheService = null,
        ICatalogKeywordReadPathScheduler? scheduler = null) =>
        new(
            new FakeMovieRepository(movie),
            new FakeMovieRegionalReleaseRepository(regionalRelease: null),
            Options.Create(new ReleaseRegionOptions { DefaultRegion = "TR" }),
            scheduler ?? new NoOpCatalogKeywordReadPathScheduler(),
            movieDataProvider,
            catalogProviderUpsertService,
            cacheService ?? new NoOpCacheService());

    private static Movie CreateMovie(int? tmdbCollectionId, string? collectionName = null) =>
        new()
        {
            Id = MovieId,
            TmdbId = 671,
            Title = "Harry Potter and the Philosopher's Stone",
            TmdbCollectionId = tmdbCollectionId,
            CollectionName = collectionName,
            ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-20)),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    private static MovieProviderDetails CreateProviderDetails(int tmdbCollectionId, string collectionName) =>
        new(
            ExternalId: "671",
            TmdbId: 671,
            TvdbId: null,
            ImdbId: null,
            Title: "Harry Potter and the Philosopher's Stone",
            OriginalTitle: null,
            Overview: null,
            ReleaseDate: null,
            RuntimeMinutes: null,
            PosterPath: null,
            BackdropPath: null,
            OriginalLanguage: null,
            VoteAverage: 0,
            VoteCount: 0,
            Genres: [],
            TmdbCollectionId: tmdbCollectionId,
            CollectionName: collectionName,
            CollectionPosterPath: "/collection-poster.jpg",
            CollectionBackdropPath: "/collection-backdrop.jpg");

    private sealed class FakeMovieRepository(Movie movie) : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == movie.Id ? movie : null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult(tmdbId == movie.TmdbId ? movie : null);

        public Task<Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeMovieRegionalReleaseRepository(MovieRegionalRelease? regionalRelease)
        : IMovieRegionalReleaseRepository
    {
        public Task<MovieRegionalRelease?> GetByMovieIdAndRegionAsync(
            Guid movieId,
            string region,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(regionalRelease);

        public Task<MovieRegionalRelease> UpsertAsync(
            MovieRegionalRelease regionalRelease,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(regionalRelease);
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

    private sealed class DictionaryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? (T?)value : null);

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

    private sealed class RecordingKeywordScheduler : ICatalogKeywordReadPathScheduler
    {
        public List<Guid> MovieIds { get; } = [];

        public void ScheduleMovie(Guid movieId) => MovieIds.Add(movieId);

        public void ScheduleTvShow(Guid tvShowId)
        {
        }
    }
}
