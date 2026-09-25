using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace MovieApp.UnitTests.Localization;

public sealed class ListLocalizationStage2Tests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("en-us")]
    public void CacheKeys_KeepCanonicalFastPath_ForEnglishLocale(string contentLocale)
    {
        var criteria = new SearchCriteria(
            "inception",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var canonicalKey = UnifiedSearchCacheKeys.Create(criteria);
        var localizedKey = UnifiedSearchCacheKeys.Create(criteria, contentLocale);

        Assert.Equal(canonicalKey, localizedKey);
        Assert.DoesNotContain(":loc:", localizedKey);
    }

    [Fact]
    public void CacheKeys_IsolateSpanishLocale_ForSearchAndHome()
    {
        var criteria = new SearchCriteria(
            "inception",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);
        var userId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var searchKey = UnifiedSearchCacheKeys.Create(criteria, ContentLocaleResolver.SpanishSpain);
        var homeKey = HomeCacheKeys.Create(userId, SearchContentType.All, 10, ContentLocaleResolver.SpanishSpain);

        Assert.EndsWith(":loc:es-es", searchKey);
        Assert.EndsWith(":loc:es-es", homeKey);
        Assert.NotEqual(
            UnifiedSearchCacheKeys.Create(criteria, ContentLocaleResolver.TurkishTurkey),
            searchKey);
    }

    [Fact]
    public void CacheKeys_IsolateTurkishLocale_ForSearchAndHome()
    {
        var criteria = new SearchCriteria(
            "inception",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var searchKey = UnifiedSearchCacheKeys.Create(criteria, ContentLocaleResolver.TurkishTurkey);
        var homeKey = HomeCacheKeys.Create(userId, SearchContentType.All, 10, ContentLocaleResolver.TurkishTurkey);

        Assert.EndsWith(":loc:tr-tr", searchKey);
        Assert.EndsWith(":loc:tr-tr", homeKey);
        Assert.NotEqual(
            UnifiedSearchCacheKeys.Create(criteria, ContentLocaleResolver.EnglishUnitedStates),
            searchKey);
    }

    [Fact]
    public async Task SummaryOverlay_ReturnsCanonicalUnchanged_ForEnglishLocale()
    {
        var cache = new InMemoryCacheService();
        var service = CreateSummaryOverlayService(cache);
        var canonical = CreateSearchPage("Interstellar", "English overview");

        var result = await service.ApplyToSearchItemsAsync(
            canonical,
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(canonical, result);
    }

    [Fact]
    public async Task SummaryOverlay_UsesCachedTurkishTitle_AndFallsBackToCanonicalOverview()
    {
        var cache = new InMemoryCacheService();
        await cache.SetAsync(
            DetailLocalizationCacheKeys.Movie(157336, ContentLocaleResolver.TurkishTurkey),
            new DetailLocalizationCacheEntry<MovieDetailLocalizationData>
            {
                Data = new MovieDetailLocalizationData("Yıldızlararası", null, null)
            });

        var service = CreateSummaryOverlayService(cache);
        var canonical = CreateSearchPage("Interstellar", "English overview");

        var result = await service.ApplyToSearchItemsAsync(
            canonical,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Yıldızlararası", result.Items[0].Title);
        Assert.Equal("English overview", result.Items[0].Overview);
    }

    [Fact]
    public async Task SummaryOverlay_FetchesLocalizationOverlayOncePerItem()
    {
        var cache = new CountingCacheService();
        await cache.SetAsync(
            DetailLocalizationCacheKeys.Movie(157336, ContentLocaleResolver.TurkishTurkey),
            new DetailLocalizationCacheEntry<MovieDetailLocalizationData>
            {
                Data = new MovieDetailLocalizationData("Yıldızlararası", "Turkish overview", null)
            });

        var service = CreateSummaryOverlayService(cache);
        var canonical = CreateSearchPage("Interstellar", "English overview");

        var result = await service.ApplyToSearchItemsAsync(canonical, ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, cache.GetCallCount);
        Assert.Equal("Yıldızlararası", result.Items[0].Title);
        Assert.Equal("Turkish overview", result.Items[0].Overview);
    }

    [Fact]
    public async Task SummaryOverlay_LoadsListTitlesConcurrentlyAndPreservesOrder()
    {
        var cache = new DelayingCacheService();
        await cache.SetAsync(
            DetailLocalizationCacheKeys.Movie(101, ContentLocaleResolver.TurkishTurkey),
            new DetailLocalizationCacheEntry<MovieDetailLocalizationData>
            {
                Data = new MovieDetailLocalizationData("Bir", null, null)
            });
        await cache.SetAsync(
            DetailLocalizationCacheKeys.Movie(202, ContentLocaleResolver.TurkishTurkey),
            new DetailLocalizationCacheEntry<MovieDetailLocalizationData>
            {
                Data = new MovieDetailLocalizationData("İki", null, null)
            });

        var service = CreateSummaryOverlayService(cache);
        var canonical = new PaginatedResult<SearchItem>(
            [
                CreateMovieItem(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "One", 101),
                CreateMovieItem(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Two", 202)
            ],
            1,
            20,
            2,
            1);

        var result = await service.ApplyToSearchItemsAsync(canonical, ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Bir", result.Items[0].Title);
        Assert.Equal("İki", result.Items[1].Title);
        Assert.True(cache.MaxInFlight >= 2);
    }

    [Fact]
    public async Task SummaryOverlay_KeepsCanonical_WhenTurkishCacheMisses()
    {
        var service = CreateSummaryOverlayService(new InMemoryCacheService());
        var canonical = CreateSearchPage("Interstellar", "English overview");

        var result = await service.ApplyToSearchItemsAsync(
            canonical,
            ContentLocaleResolver.TurkishTurkey);

        Assert.Equal("Interstellar", result.Items[0].Title);
        Assert.Equal("English overview", result.Items[0].Overview);
    }

    [Fact]
    public async Task ProviderIngestion_UsesCanonicalSummariesForDatabaseWrites_OnTurkishLocale()
    {
        const int tmdbId = 157336;
        var movieRepository = new TrackingMovieRepository();
        var service = new UnifiedSearchProviderIngestionService(
            new CanonicalMovieDataProvider(tmdbId, "Interstellar"),
            new NoOpTvShowDataProvider(),
            new NoOpPersonDataProvider(),
            new LocalizedMovieListDataProvider(tmdbId, "Yıldızlararası"),
            movieRepository,
            new NoOpTvShowRepository(),
            new NoOpPersonRepository(),
            NullLogger<UnifiedSearchProviderIngestionService>.Instance);

        var criteria = new SearchCriteria(
            "interstellar",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        var result = await service.IngestAsync(criteria, ContentLocaleResolver.TurkishTurkey);

        Assert.True(result.IsFullySuccessful);
        Assert.NotNull(result.Result);
        Assert.Equal("Yıldızlararası", result.Result!.Items[0].Title);
        Assert.Single(movieRepository.IngestedTitles);
        Assert.Equal("Interstellar", movieRepository.IngestedTitles[0]);
    }

    [Fact]
    public async Task ProviderIngestion_BoundsTmdbCalls_ForTurkishMovieSearch()
    {
        var movieProvider = new CountingMovieDataProvider();
        var localizedProvider = new CountingLocalizedListDataProvider();
        var service = new UnifiedSearchProviderIngestionService(
            movieProvider,
            new NoOpTvShowDataProvider(),
            new NoOpPersonDataProvider(),
            localizedProvider,
            new NoOpMovieRepository(),
            new NoOpTvShowRepository(),
            new NoOpPersonRepository(),
            NullLogger<UnifiedSearchProviderIngestionService>.Instance);

        var criteria = new SearchCriteria(
            "interstellar",
            SearchContentType.Movie,
            null,
            null,
            null,
            null,
            SearchSortOption.Relevance,
            1,
            20);

        await service.IngestAsync(criteria, ContentLocaleResolver.TurkishTurkey);

        Assert.Equal(1, movieProvider.SearchCalls);
        Assert.Equal(1, localizedProvider.MovieSearchCalls);
    }

    private static SummaryLocalizationOverlayService CreateSummaryOverlayService(ICacheService cache) =>
        new(cache, new StubMovieRepository(), new StubTvShowRepository());

    private static SearchItem CreateMovieItem(Guid id, string title, int tmdbId) =>
        new(
            id,
            "movie",
            title,
            null,
            "overview",
            null,
            null,
            null,
            8m,
            10,
            2020,
            tmdbId);

    private static PaginatedResult<SearchItem> CreateSearchPage(string title, string overview) =>
        new(
            [
                new SearchItem(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    "movie",
                    title,
                    null,
                    overview,
                    null,
                    null,
                    null,
                    8.7m,
                    100,
                    2014,
                    157336)
            ],
            1,
            20,
            1,
            1);

    private sealed class DelayingCacheService : InMemoryCacheService
    {
        private int _inFlight;

        public int MaxInFlight { get; private set; }

        public override async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            var current = Interlocked.Increment(ref _inFlight);
            lock (this)
            {
                if (current > MaxInFlight)
                {
                    MaxInFlight = current;
                }
            }

            try
            {
                await Task.Delay(40, cancellationToken);
                return await base.GetAsync<T>(key, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    private sealed class CountingCacheService : InMemoryCacheService
    {
        public int GetCallCount { get; private set; }

        public override Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            GetCallCount++;
            return base.GetAsync<T>(key, cancellationToken);
        }
    }

    private class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public virtual Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)

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

    private sealed class StubMovieRepository : IMovieRepository
    {
        public Task<IReadOnlyDictionary<Guid, int>> GetTmdbIdsByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
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

    private sealed class StubTvShowRepository : ITvShowRepository
    {
        public Task<IReadOnlyDictionary<Guid, int>> GetTmdbIdsByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
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

    private sealed class TrackingMovieRepository : IMovieRepository
    {
        public List<string> IngestedTitles { get; } = [];

        public Task<IReadOnlyDictionary<Guid, int>> GetTmdbIdsByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            IngestedTitles.AddRange(summaries.Select(summary => summary.Title));
            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid()));
        }

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
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

    private sealed class NoOpMovieRepository : IMovieRepository
    {
        public Task<IReadOnlyDictionary<Guid, int>> GetTmdbIdsByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue)
                    .ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid()));

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
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
        public Task<IReadOnlyDictionary<Guid, int>> GetTmdbIdsByIdsAsync(
            IReadOnlyList<Guid> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());

        public Task<IReadOnlyDictionary<int, Guid>> GetExistingIdsByTmdbIdsAsync(
            IReadOnlyList<int> tmdbIds,
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

    private sealed class NoOpPersonRepository : IPersonRepository
    {
        public Task<Person?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Person?>(null);

        public Task<Person> UpsertFromProviderAsync(
            int tmdbId,
            string name,
            string? profilePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<PersonProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());
    }

    private sealed class CanonicalMovieDataProvider(int tmdbId, string title) : IMovieDataProvider
    {
        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MovieProviderSearchResult(
                [new($"tmdb-{tmdbId}", tmdbId, null, null, title, null, null, null, 8m, 100)],
                page,
                pageSize,
                1,
                1));

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(string externalId, bool includeKeywords = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieProviderDetails?>(null);
    }

    private sealed class LocalizedMovieListDataProvider(int tmdbId, string title) : ILocalizedListDataProvider
    {
        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MovieProviderSearchResult(
                [new($"tmdb-{tmdbId}", tmdbId, null, null, title, null, null, null, 8m, 100)],
                page,
                pageSize,
                1,
                1));

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PersonProviderSearchResult> SearchPersonsAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> AdvancedDiscoverMoviesAsync(
            AdvancedDiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
            AdvancedDiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CountingMovieDataProvider : IMovieDataProvider
    {
        public int SearchCalls { get; private set; }

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(new MovieProviderSearchResult(
                [new("tmdb-1", 1, null, null, "Interstellar", null, null, null, 8m, 100)],
                page,
                pageSize,
                1,
                1));
        }

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderDetails?> GetMovieAsync(string externalId, bool includeKeywords = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieProviderDetails?>(null);
    }

    private sealed class CountingLocalizedListDataProvider : ILocalizedListDataProvider
    {
        public int MovieSearchCalls { get; private set; }

        public Task<MovieProviderSearchResult> SearchMoviesAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default)
        {
            MovieSearchCalls++;
            return Task.FromResult(new MovieProviderSearchResult(
                [new("tmdb-1", 1, null, null, "Yıldızlararası", null, null, null, 8m, 100)],
                page,
                pageSize,
                1,
                1));
        }

        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PersonProviderSearchResult> SearchPersonsAsync(
            string query,
            int page,
            int pageSize,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> DiscoverMoviesAsync(
            DiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieProviderSearchResult> AdvancedDiscoverMoviesAsync(
            AdvancedDiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> AdvancedDiscoverTvShowsAsync(
            AdvancedDiscoverProviderCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpTvShowDataProvider : ITvShowDataProvider
    {
        public Task<TvShowProviderSearchResult> SearchTvShowsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderSearchResult> DiscoverTvShowsAsync(
            DiscoverProviderCriteria criteria,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TvShowProviderDetails?> GetTvShowAsync(string externalId, bool includeKeywords = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<TvShowProviderDetails?>(null);

        public Task<SeasonProviderDetails?> GetSeasonAsync(
            string externalId,
            int seasonNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SeasonProviderDetails?>(null);

        public Task<EpisodeProviderDetails?> GetEpisodeAsync(
            string externalId,
            int seasonNumber,
            int episodeNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EpisodeProviderDetails?>(null);
    }

    private sealed class NoOpPersonDataProvider : IPersonDataProvider
    {
        public Task<PersonProviderDetails?> GetPersonAsync(int tmdbPersonId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PersonProviderDetails?>(null);

        public Task<PersonProviderSearchResult> SearchPersonsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
