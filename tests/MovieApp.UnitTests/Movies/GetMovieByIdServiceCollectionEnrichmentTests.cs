using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;

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

    private static GetMovieByIdService CreateService(
        Movie movie,
        StubMovieDataProvider movieDataProvider,
        ICatalogProviderUpsertService catalogProviderUpsertService) =>
        new(
            new FakeMovieRepository(movie),
            new FakeMovieRegionalReleaseRepository(regionalRelease: null),
            Options.Create(new ReleaseRegionOptions { DefaultRegion = "TR" }),
            new NoOpCatalogKeywordIngestionService(),
            movieDataProvider,
            catalogProviderUpsertService,
            new NoOpCacheService());

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

    private sealed class NoOpCatalogKeywordIngestionService : ICatalogKeywordIngestionService
    {
        public Task TryEnrichMovieKeywordsAsync(
            Guid movieId,
            bool refreshKeywords,
            IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task TryEnrichTvShowKeywordsAsync(
            Guid tvShowId,
            bool refreshKeywords,
            IReadOnlyList<ProviderKeywordSummary>? prefetchedKeywords = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
}
