using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Services.Keywords;
using MovieApp.Application.Services.Movies;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Keywords;

public sealed class SummaryIngestionKeywordRegressionTests
{
    [Fact]
    public async Task SearchMoviesServicePerformsZeroKeywordProviderCalls()
    {
        var keywordProvider = new TrackingKeywordsProvider();
        var service = new SearchMoviesService(
            new FakeMovieDataProvider([CreateSummary(1)]),
            new KeywordAwareCatalogProviderUpsertService(
                new NoOpMovieRepository(),
                keywordProvider),
            new NoOpCacheService(),
            Options.Create(new SearchOptions
            {
                MaxProviderDetailFetchesPerContentType = 5,
                MaxConcurrentProviderHttpRequests = 2
            }),
            NullLogger<SearchMoviesService>.Instance);

        await service.SearchAsync(new MovieSearchRequest("batman", 1, 20));

        Assert.Equal(0, keywordProvider.MovieCalls);
        Assert.Equal(0, keywordProvider.TvCalls);
    }

    private static MovieProviderSummary CreateSummary(int tmdbId) =>
        new(
            $"fake-{tmdbId}",
            tmdbId,
            null,
            $"tt{tmdbId:D7}",
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
            Task.FromResult(new MovieProviderSearchResult(summaries, page, pageSize, summaries.Count, 1));

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(string externalId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieProviderDetails?>(new MovieProviderDetails(
                externalId,
                1,
                null,
                null,
                "Movie",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                []));
    }

    private sealed class NoOpMovieRepository : IMovieRepository
    {
        public Task<Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Movie?>(null);

        public Task<Movie> UpsertFromProviderAsync(MovieProviderDetails details, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Movie { Id = Guid.NewGuid(), TmdbId = details.TmdbId, Title = details.Title });

        public Task<IReadOnlyList<Movie>> UpsertFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Movie>>(
                details
                    .Select(detail => new Movie
                    {
                        Id = Guid.NewGuid(),
                        TmdbId = detail.TmdbId,
                        Title = detail.Title
                    })
                    .ToList());
    }

    private sealed class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TrackingKeywordsProvider : IKeywordsProvider
    {
        public int MovieCalls { get; private set; }

        public int TvCalls { get; private set; }

        public Task<IReadOnlyList<ProviderKeywordSummary>> GetMovieKeywordsAsync(
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            MovieCalls++;
            return Task.FromResult<IReadOnlyList<ProviderKeywordSummary>>([]);
        }

        public Task<IReadOnlyList<ProviderKeywordSummary>> GetTvShowKeywordsAsync(
            int tmdbId,
            CancellationToken cancellationToken = default)
        {
            TvCalls++;
            return Task.FromResult<IReadOnlyList<ProviderKeywordSummary>>([]);
        }
    }

    private sealed class KeywordAwareCatalogProviderUpsertService(
        IMovieRepository movieRepository,
        IKeywordsProvider keywordsProvider) : ICatalogProviderUpsertService
    {
        private readonly CatalogProviderUpsertService _inner = new(
            movieRepository,
            new UnsupportedTvShowRepository(),
            new CatalogKeywordIngestionService(
                keywordsProvider,
                new UnsupportedKeywordCatalogRepository(),
                NullLogger<CatalogKeywordIngestionService>.Instance),
            new NoOpMovieCatalogDetailsCacheInvalidator());

        public Task<Movie> UpsertMovieFromProviderAsync(
            MovieProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            _inner.UpsertMovieFromProviderAsync(details, enrichKeywords, cancellationToken);

        public Task<IReadOnlyList<Movie>> UpsertMoviesFromProviderBatchAsync(
            IReadOnlyList<MovieProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            _inner.UpsertMoviesFromProviderBatchAsync(details, enrichKeywords, cancellationToken);

        public Task<TvShow> UpsertTvShowFromProviderAsync(
            TvShowProviderDetails details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TvShow>> UpsertTvShowsFromProviderBatchAsync(
            IReadOnlyList<TvShowProviderDetails> details,
            bool enrichKeywords = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnsupportedTvShowRepository : ITvShowRepository
    {
        public Task<TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShow> UpsertFromProviderAsync(TvShowProviderDetails details, CancellationToken cancellationToken = default) =>
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

    private sealed class UnsupportedKeywordCatalogRepository : IKeywordCatalogRepository
    {
        public Task<Application.Models.Keywords.KeywordEnrichmentTarget?> GetMovieKeywordTargetAsync(
            Guid movieId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Application.Models.Keywords.KeywordEnrichmentTarget?>(
                new Application.Models.Keywords.KeywordEnrichmentTarget(1, null));

        public Task<Application.Models.Keywords.KeywordEnrichmentTarget?> GetTvShowKeywordTargetAsync(
            Guid tvShowId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SyncMovieKeywordsAsync(
            Guid movieId,
            IReadOnlyList<ProviderKeywordSummary> keywords,
            DateTime syncedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SyncTvShowKeywordsAsync(
            Guid tvShowId,
            IReadOnlyList<ProviderKeywordSummary> keywords,
            DateTime syncedAtUtc,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpMovieCatalogDetailsCacheInvalidator : IMovieCatalogDetailsCacheInvalidator
    {
        public Task InvalidateAsync(Guid movieId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
