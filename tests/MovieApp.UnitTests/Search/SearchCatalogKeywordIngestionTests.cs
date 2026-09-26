using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Keywords;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Movies;
using MovieApp.UnitTests.Keywords;

namespace MovieApp.UnitTests.Search;

public sealed class SearchCatalogKeywordIngestionTests
{
    [Fact]
    public async Task SearchAsyncSyncsPrefetchedKeywordsWithoutSeparateProviderFetch()
    {
        var keywordsProvider = new TrackingKeywordsProvider();
        var keywordRepository = new FakeKeywordCatalogRepository
        {
            MovieTarget = new KeywordEnrichmentTarget(1, null)
        };
        var upsertService = new CatalogProviderUpsertService(
            new SingleMovieRepository(),
            new NoOpTvShowRepository(),
            new CatalogKeywordIngestionService(
                keywordsProvider,
                keywordRepository,
                NullLogger<CatalogKeywordIngestionService>.Instance),
            new NoOpMovieCatalogDetailsCacheInvalidator(),
            new NoOpContentSearchTitleSynchronizer());

        var service = new SearchMoviesService(
            new KeywordReturningMovieDataProvider(),
            upsertService,
            new FakeCacheService(null),
            Options.Create(new SearchOptions
            {
                MaxProviderDetailFetchesPerContentType = 20,
                MaxConcurrentProviderHttpRequests = 4
            }),
            NullLogger<SearchMoviesService>.Instance);

        await service.SearchAsync(new MovieSearchRequest("ingest", 1, 20));

        Assert.Equal(0, keywordsProvider.MovieCalls);
        Assert.Equal(1, keywordRepository.MovieSyncCalls);
    }

    private sealed class KeywordReturningMovieDataProvider : IMovieDataProvider
    {
        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MovieProviderSearchResult(
                [
                    new MovieProviderSummary(
                        "fake-1",
                        1,
                        null,
                        "tt0000001",
                        "Movie",
                        "Overview",
                        new DateOnly(2020, 1, 1),
                        "/poster.jpg",
                        7m,
                        10)
                ],
                page,
                pageSize,
                1,
                1));

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(
            string externalId,
            bool includeKeywords = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieProviderDetails?>(new MovieProviderDetails(
                externalId,
                1,
                null,
                "tt0000001",
                "Movie",
                "Movie",
                "Overview",
                new DateOnly(2020, 1, 1),
                100,
                "/poster.jpg",
                "/backdrop.jpg",
                "en",
                7m,
                10,
                ["Drama"],
                Keywords: [new ProviderKeywordSummary(99, "heist")]));
    }

    private sealed class NoOpTvShowRepository : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShow?>(null);

        public Task<TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TvShow>> UpsertFromProviderBatchAsync(
            IReadOnlyList<TvShowProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SingleMovieRepository : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Movie>> UpsertFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Movie>>(
            [
                new Movie
                {
                    Id = Guid.NewGuid(),
                    TmdbId = details[0].TmdbId,
                    Title = details[0].Title
                }
            ]);
    }

    private sealed class TrackingKeywordsProvider : IKeywordsProvider
    {
        public int MovieCalls { get; private set; }

        public Task<IReadOnlyList<ProviderKeywordSummary>> GetMovieKeywordsAsync(
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            MovieCalls++;
            return Task.FromResult<IReadOnlyList<ProviderKeywordSummary>>([]);
        }

        public Task<IReadOnlyList<ProviderKeywordSummary>> GetTvShowKeywordsAsync(
            int tmdbId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeKeywordCatalogRepository : IKeywordCatalogRepository
    {
        public KeywordEnrichmentTarget? MovieTarget { get; init; }

        public int MovieSyncCalls { get; private set; }

        public Task<KeywordEnrichmentTarget?> GetMovieKeywordTargetAsync(
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MovieTarget);

        public Task<KeywordEnrichmentTarget?> GetTvShowKeywordTargetAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<KeywordEnrichmentTarget?>(null);

        public Task SyncMovieKeywordsAsync(
            Guid movieId,
            IReadOnlyList<ProviderKeywordSummary> keywords,
            DateTime syncedAtUtc,
            CancellationToken cancellationToken = default)
        {
            MovieSyncCalls++;
            return Task.CompletedTask;
        }

        public Task SyncTvShowKeywordsAsync(
            Guid tvShowId,
            IReadOnlyList<ProviderKeywordSummary> keywords,
            DateTime syncedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpMovieCatalogDetailsCacheInvalidator : IMovieCatalogDetailsCacheInvalidator
    {
        public Task InvalidateAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
