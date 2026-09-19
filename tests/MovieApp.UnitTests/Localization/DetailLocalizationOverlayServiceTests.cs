using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Collections;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.People;
using MovieApp.Application.Models.TvShows;
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
    public async Task ApplyPersonOverlayAsync_MergesTurkishFilmography_FromListOverlay()
    {
        var provider = new RecordingLocalizedDetailDataProvider
        {
            PersonLocalization = new PersonDetailLocalizationData(
                Biography: "Turkish biography",
                Filmography:
                [
                    new PersonFilmographyLocalizationItem("movie", 550, "Dövüş Kulübü", "Anlatıcı"),
                ])
        };
        var service = CreateService(provider, new InMemoryCacheService());
        var canonical = CreatePerson();

        var result = await service.ApplyPersonOverlayAsync(
            canonical,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Turkish biography", result.Biography);
        Assert.Equal("Dövüş Kulübü", result.Filmography[0].Title);
        Assert.Equal("Anlatıcı", result.Filmography[0].Character);
    }

    [Fact]
    public async Task ApplySeasonOverlayAsync_MergesTurkishSeasonAndEpisodeFields()
    {
        var provider = new RecordingLocalizedDetailDataProvider
        {
            SeasonLocalization = new TvSeasonDetailLocalizationData(
                Name: "Sezon 1",
                Overview: "Turkish season overview",
                Episodes:
                [
                    new TvSeasonEpisodeLocalizationItem(1, "Pilot", "Turkish episode overview"),
                ])
        };
        var service = CreateService(provider, new InMemoryCacheService());
        var canonical = CreateSeason();

        var result = await service.ApplySeasonOverlayAsync(
            canonical,
            1399,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Sezon 1", result.Name);
        Assert.Equal("Turkish season overview", result.Overview);
        Assert.Equal("Pilot", result.Episodes[0].Name);
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

    private static PersonDetailResult CreatePerson() =>
        new(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            287,
            "Brad Pitt",
            "/profile.jpg",
            "English biography",
            new DateOnly(1963, 12, 18),
            null,
            null,
            "Acting",
            [
                new PersonFilmographyEntryResult(
                    "movie",
                    null,
                    550,
                    "Fight Club",
                    "/poster.jpg",
                    "The Narrator",
                    new DateOnly(1999, 10, 15))
            ]);

    private static SeasonResult CreateSeason() =>
        new(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            1,
            "Season 1",
            "English season overview",
            new DateOnly(2011, 4, 17),
            1,
            "/season-poster.jpg",
            [
                new EpisodeSummaryResult(
                    Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    1,
                    "Winter Is Coming",
                    new DateOnly(2011, 4, 17),
                    62,
                    "/still.jpg",
                    8.1m,
                    100)
            ]);

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

        public PersonDetailLocalizationData? PersonLocalization { get; init; }

        public TvSeasonDetailLocalizationData? SeasonLocalization { get; init; }

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

        public Task<TvSeasonDetailLocalizationData?> GetTvSeasonLocalizationAsync(
            int tmdbTvId,
            int seasonNumber,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(SeasonLocalization);

        public Task<PersonDetailLocalizationData?> GetPersonLocalizationAsync(
            int tmdbPersonId,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PersonLocalization);

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
