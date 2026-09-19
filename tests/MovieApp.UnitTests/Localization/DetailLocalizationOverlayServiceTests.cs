using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Collections;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Services.Localization;

namespace MovieApp.UnitTests.Localization;

public sealed class DetailLocalizationOverlayServiceTests
{
    [Fact]
    public async Task ApplyMovieOverlayAsync_ReturnsCanonicalUnchanged_ForEnglishLocale()
    {
        var provider = new RecordingLocalizedDetailDataProvider();
        var service = CreateService(provider, new InMemoryCacheService());
        var canonical = CreateMovie("Interstellar", "English overview");

        var result = await service.ApplyMovieOverlayAsync(
            canonical,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(canonical, result);
        Assert.Equal(0, provider.MovieCalls);
    }

    [Fact]
    public async Task ApplyMovieOverlayAsync_MergesTurkishFields_AndFallsBackToCanonicalWhenMissing()
    {
        var provider = new RecordingLocalizedDetailDataProvider
        {
            MovieLocalization = new MovieDetailLocalizationData(
                Title: "Yıldızlararası",
                Overview: null,
                CollectionName: null)
        };
        var cache = new InMemoryCacheService();
        var service = CreateService(provider, cache);
        var canonical = CreateMovie("Interstellar", "English overview");

        var result = await service.ApplyMovieOverlayAsync(
            canonical,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Yıldızlararası", result.Title);
        Assert.Equal("Interstellar", result.OriginalTitle);
        Assert.Equal("English overview", result.Overview);
        Assert.Equal(1, provider.MovieCalls);
        Assert.Contains(
            DetailLocalizationCacheKeys.Movie(157336, ContentLocaleResolver.TurkishTurkey),
            cache.StoredKeys);
    }

    [Fact]
    public async Task ApplyMovieOverlayAsync_UsesCachedOverlay_OnSecondRequest()
    {
        var provider = new RecordingLocalizedDetailDataProvider
        {
            MovieLocalization = new MovieDetailLocalizationData("Yıldızlararası", "Turkish overview", null)
        };
        var cache = new InMemoryCacheService();
        var service = CreateService(provider, cache);
        var canonical = CreateMovie("Interstellar", "English overview");

        await service.ApplyMovieOverlayAsync(canonical, ContentLocaleResolver.TurkishTurkey);
        await service.ApplyMovieOverlayAsync(canonical, ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, provider.MovieCalls);
    }

    [Fact]
    public async Task ApplyMovieOverlayAsync_IsolatesCache_ByLocale()
    {
        var provider = new RecordingLocalizedDetailDataProvider
        {
            MovieLocalization = new MovieDetailLocalizationData("Yıldızlararası", "Turkish overview", null)
        };
        var cache = new InMemoryCacheService();
        var service = CreateService(provider, cache);
        var canonical = CreateMovie("Interstellar", "English overview");

        await service.ApplyMovieOverlayAsync(canonical, ContentLocaleResolver.TurkishTurkey);

        Assert.Contains(
            DetailLocalizationCacheKeys.Movie(157336, ContentLocaleResolver.TurkishTurkey),
            cache.StoredKeys);
        Assert.DoesNotContain(
            DetailLocalizationCacheKeys.Movie(157336, ContentLocaleResolver.EnglishUnitedStates),
            cache.StoredKeys);
    }

    private static DetailLocalizationOverlayService CreateService(
        ILocalizedDetailDataProvider provider,
        ICacheService cache) =>
        new(provider, cache);

    private static MovieDetailsResult CreateMovie(string title, string overview) =>
        new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            157336,
            null,
            "tt0816692",
            title,
            "Interstellar",
            overview,
            new DateOnly(2014, 11, 7),
            169,
            "/poster.jpg",
            "/backdrop.jpg",
            "en",
            8.7m,
            100,
            ["Adventure"],
            null,
            true,
            false,
            false);

    private sealed class RecordingLocalizedDetailDataProvider : ILocalizedDetailDataProvider
    {
        public MovieDetailLocalizationData? MovieLocalization { get; init; }

        public int MovieCalls { get; private set; }

        public Task<MovieDetailLocalizationData?> GetMovieLocalizationAsync(
            int tmdbId,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            MovieCalls++;
            return Task.FromResult(MovieLocalization);
        }

        public Task<TvShowDetailLocalizationData?> GetTvShowLocalizationAsync(
            int tmdbId,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowDetailLocalizationData?>(null);

        public Task<PersonDetailLocalizationData?> GetPersonLocalizationAsync(
            int tmdbPersonId,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PersonDetailLocalizationData?>(null);

        public Task<CollectionDetailLocalizationData?> GetCollectionLocalizationAsync(
            int tmdbCollectionId,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CollectionDetailLocalizationData?>(null);
    }

    private sealed class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> StoredKeys => _entries.Keys;

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
}
