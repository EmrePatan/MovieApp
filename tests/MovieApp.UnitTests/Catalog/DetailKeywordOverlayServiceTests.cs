using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Catalog;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.Catalog;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Catalog;

public sealed class DetailKeywordOverlayServiceTests
{
    [Fact]
    public async Task ApplyMovieKeywordsAsync_FirstRequestLoadsRepositoryAndCaches()
    {
        var movieId = Guid.NewGuid();
        var keywordId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var repository = new CountingKeywordRepository(
            movieKeywords: [new CatalogKeywordSummary(keywordId, "time travel")]);
        var cache = new InMemoryCacheService();
        var service = new DetailKeywordOverlayService(repository, cache);

        var result = await service.ApplyMovieKeywordsAsync(
            movieId,
            CreateMovie(movieId),
            ContentLocaleResolver.SpanishSpain,
            CancellationToken.None);

        Assert.Equal(1, repository.MovieCallCount);
        Assert.Single(result.Keywords);
        Assert.Equal(ContentLocaleResolver.SpanishSpain, repository.LastMovieLocale);
        Assert.NotNull(await cache.GetAsync<DetailKeywordCacheEntry>(
            DetailKeywordCacheKeys.Movie(movieId, ContentLocaleResolver.SpanishSpain)));
    }

    [Fact]
    public async Task ApplyMovieKeywordsAsync_SecondRequestWithSameMovieAndLocaleSkipsRepository()
    {
        var movieId = Guid.NewGuid();
        var repository = new CountingKeywordRepository(
            movieKeywords: [new CatalogKeywordSummary(Guid.NewGuid(), "friendship")]);
        var cache = new InMemoryCacheService();
        var service = new DetailKeywordOverlayService(repository, cache);
        var canonical = CreateMovie(movieId);

        await service.ApplyMovieKeywordsAsync(movieId, canonical, ContentLocaleResolver.TurkishTurkey, CancellationToken.None);
        await service.ApplyMovieKeywordsAsync(movieId, canonical, ContentLocaleResolver.TurkishTurkey, CancellationToken.None);

        Assert.Equal(1, repository.MovieCallCount);
    }

    [Fact]
    public async Task ApplyMovieKeywordsAsync_DifferentLocaleUsesSeparateCacheEntry()
    {
        var movieId = Guid.NewGuid();
        var repository = new CountingKeywordRepository(
            movieKeywords: [new CatalogKeywordSummary(Guid.NewGuid(), "keyword")]);
        var cache = new InMemoryCacheService();
        var service = new DetailKeywordOverlayService(repository, cache);
        var canonical = CreateMovie(movieId);

        await service.ApplyMovieKeywordsAsync(movieId, canonical, ContentLocaleResolver.TurkishTurkey, CancellationToken.None);
        await service.ApplyMovieKeywordsAsync(movieId, canonical, ContentLocaleResolver.EnglishUnitedStates, CancellationToken.None);

        Assert.Equal(2, repository.MovieCallCount);
    }

    [Fact]
    public async Task ApplyMovieKeywordsAsync_DifferentMovieIdUsesSeparateCacheEntry()
    {
        var repository = new CountingKeywordRepository(
            movieKeywords: [new CatalogKeywordSummary(Guid.NewGuid(), "keyword")]);
        var cache = new InMemoryCacheService();
        var service = new DetailKeywordOverlayService(repository, cache);
        var firstMovieId = Guid.NewGuid();
        var secondMovieId = Guid.NewGuid();

        await service.ApplyMovieKeywordsAsync(firstMovieId, CreateMovie(firstMovieId), ContentLocaleResolver.EnglishUnitedStates, CancellationToken.None);
        await service.ApplyMovieKeywordsAsync(secondMovieId, CreateMovie(secondMovieId), ContentLocaleResolver.EnglishUnitedStates, CancellationToken.None);

        Assert.Equal(2, repository.MovieCallCount);
    }

    [Fact]
    public async Task ApplyTvShowKeywordsAsync_SecondRequestSkipsRepository()
    {
        var tvShowId = Guid.NewGuid();
        var repository = new CountingKeywordRepository(
            tvKeywords: [new CatalogKeywordSummary(Guid.NewGuid(), "drama")]);
        var cache = new InMemoryCacheService();
        var service = new DetailKeywordOverlayService(repository, cache);
        var canonical = CreateTvShow(tvShowId);

        await service.ApplyTvShowKeywordsAsync(tvShowId, canonical, ContentLocaleResolver.GermanGermany, CancellationToken.None);
        await service.ApplyTvShowKeywordsAsync(tvShowId, canonical, ContentLocaleResolver.GermanGermany, CancellationToken.None);

        Assert.Equal(1, repository.TvShowCallCount);
    }

    [Fact]
    public async Task ApplyMovieKeywordsAsync_CachesEmptyKeywordList()
    {
        var movieId = Guid.NewGuid();
        var repository = new CountingKeywordRepository(movieKeywords: []);
        var cache = new InMemoryCacheService();
        var service = new DetailKeywordOverlayService(repository, cache);
        var canonical = CreateMovie(movieId);

        var first = await service.ApplyMovieKeywordsAsync(movieId, canonical, ContentLocaleResolver.EnglishUnitedStates, CancellationToken.None);
        var second = await service.ApplyMovieKeywordsAsync(movieId, canonical, ContentLocaleResolver.EnglishUnitedStates, CancellationToken.None);

        Assert.Empty(first.Keywords);
        Assert.Empty(second.Keywords);
        Assert.Equal(1, repository.MovieCallCount);
    }

    [Fact]
    public async Task ApplyMovieKeywordsAsync_NormalizedLocaleAliasesShareCacheEntry()
    {
        var movieId = Guid.NewGuid();
        var repository = new CountingKeywordRepository(
            movieKeywords: [new CatalogKeywordSummary(Guid.NewGuid(), "keyword")]);
        var cache = new InMemoryCacheService();
        var service = new DetailKeywordOverlayService(repository, cache);
        var canonical = CreateMovie(movieId);

        await service.ApplyMovieKeywordsAsync(movieId, canonical, "tr", CancellationToken.None);
        await service.ApplyMovieKeywordsAsync(movieId, canonical, ContentLocaleResolver.TurkishTurkey, CancellationToken.None);

        Assert.Equal(1, repository.MovieCallCount);
        Assert.Equal(
            DetailKeywordCacheKeys.Movie(movieId, "tr"),
            DetailKeywordCacheKeys.Movie(movieId, ContentLocaleResolver.TurkishTurkey));
    }

    [Fact]
    public async Task ApplyMovieKeywordsAsync_DefaultsLocaleWhenMissing()
    {
        var movieId = Guid.NewGuid();
        var repository = new CountingKeywordRepository(movieKeywords: []);
        var service = new DetailKeywordOverlayService(repository, new InMemoryCacheService());

        await service.ApplyMovieKeywordsAsync(movieId, CreateMovie(movieId), contentLocale: null, CancellationToken.None);

        Assert.Equal(ContentLocaleResolver.EnglishUnitedStates, repository.LastMovieLocale);
    }

    private static MovieDetailsResult CreateMovie(Guid id) =>
        new(
            id,
            1,
            null,
            null,
            "Title",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0m,
            0,
            [],
            [],
            null,
            true,
            false,
            false);

    private static TvShowDetailsResult CreateTvShow(Guid id) =>
        new(
            id,
            1,
            null,
            null,
            "Title",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0m,
            0,
            "Ended",
            [],
            [],
            [],
            true);

    private sealed class CountingKeywordRepository : ICatalogTitleKeywordReadRepository
    {
        private readonly IReadOnlyList<CatalogKeywordSummary> _movieKeywords;
        private readonly IReadOnlyList<CatalogKeywordSummary> _tvKeywords;

        public CountingKeywordRepository(
            IReadOnlyList<CatalogKeywordSummary>? movieKeywords = null,
            IReadOnlyList<CatalogKeywordSummary>? tvKeywords = null)
        {
            _movieKeywords = movieKeywords ?? [];
            _tvKeywords = tvKeywords ?? [];
        }

        public int MovieCallCount { get; private set; }

        public int TvShowCallCount { get; private set; }

        public string? LastMovieLocale { get; private set; }

        public Task<IReadOnlyList<CatalogKeywordSummary>> GetLocalizedKeywordsForMovieAsync(
            Guid movieId,
            string contentLocale,
            int maxCount,
            CancellationToken cancellationToken = default)
        {
            MovieCallCount++;
            LastMovieLocale = contentLocale;
            return Task.FromResult(_movieKeywords);
        }

        public Task<IReadOnlyList<CatalogKeywordSummary>> GetLocalizedKeywordsForTvShowAsync(
            Guid tvShowId,
            string contentLocale,
            int maxCount,
            CancellationToken cancellationToken = default)
        {
            TvShowCallCount++;
            return Task.FromResult(_tvKeywords);
        }
    }

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);

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
