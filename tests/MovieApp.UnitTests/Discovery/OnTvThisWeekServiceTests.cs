using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Discovery;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Discovery;
using MovieApp.Application.Services.Localization;
using MovieApp.UnitTests.Search;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Discovery;

public sealed class OnTvThisWeekServiceTests
{
    [Fact]
    public async Task GetOnTvThisWeekAsyncReturnsCachedResultsWithoutCallingCatalog()
    {
        var catalog = new RecordingOnTvThisWeekCatalog();
        var cache = new OnTvThisWeekFakeCache();
        var service = CreateService(catalog, cache);
        var criteria = new OnTvThisWeekCriteria(1, 20);
        var cachedResult = new PaginatedResult<SearchItem>(
            [CreateTvItem(Guid.NewGuid(), "Airing Drama")],
            1,
            20,
            1,
            1);

        await cache.SetAsync(
            OnTvThisWeekCacheKeys.Create(criteria, ContentLocaleResolver.EnglishUnitedStates),
            new DiscoveryCacheEntry { Result = cachedResult },
            TimeSpan.FromMinutes(30));

        var result = await service.GetOnTvThisWeekAsync(criteria, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(result.Items);
        Assert.Equal(0, catalog.CallCount);
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncReturnsOnlyTvItems()
    {
        var service = CreateService(new RecordingOnTvThisWeekCatalog(), new OnTvThisWeekFakeCache());

        var result = await service.GetOnTvThisWeekAsync(new OnTvThisWeekCriteria(1, 20), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(result.Items);
        Assert.All(result.Items, item => Assert.Equal("tv", item.Type));
        Assert.Equal("Airing Drama", result.Items[0].Title);
        Assert.True(result.Items[0].VoteCount >= OnTvThisWeekQualityFilter.MinimumVoteCount);
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncThrowsWhenCatalogFails()
    {
        var catalog = new RecordingOnTvThisWeekCatalog { ShouldThrow = true };
        var service = CreateService(catalog, new OnTvThisWeekFakeCache());

        await Assert.ThrowsAsync<SearchProviderUnavailableException>(
            () => service.GetOnTvThisWeekAsync(new OnTvThisWeekCriteria(1, 20), ContentLocaleResolver.EnglishUnitedStates));
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncDoesNotDependOnFollowedCatalogPath()
    {
        var catalog = new RecordingOnTvThisWeekCatalog();
        var service = CreateService(catalog, new OnTvThisWeekFakeCache());

        await service.GetOnTvThisWeekAsync(new OnTvThisWeekCriteria(1, 20), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, catalog.CallCount);
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncExcludesTalkAndPrefersScriptedTitles()
    {
        var catalog = new RecordingOnTvThisWeekCatalog();
        var service = CreateService(catalog, new OnTvThisWeekFakeCache());

        var result = await service.GetOnTvThisWeekAsync(new OnTvThisWeekCriteria(1, 10), ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal("Airing Drama", result.Items[0].Title);
        Assert.DoesNotContain(result.Items, item => item.Title == "Late Night Talk");
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncFetchesSecondTmdbPageOnlyWhenFirstPageCannotFillSectionSize()
    {
        var catalog = new MultiPageRecordingOnTvThisWeekCatalog();
        var service = CreateService(catalog, new OnTvThisWeekFakeCache());

        var result = await service.GetOnTvThisWeekAsync(
            new OnTvThisWeekCriteria(1, 3, OnTvThisWeekPresentationIntent.HomeRail),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal([1, 2], catalog.RequestedPages);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(["Scripted One", "Scripted Two", "Scripted Three"], result.Items.Select(item => item.Title).ToArray());
        Assert.DoesNotContain(result.Items, item => item.Title.StartsWith("Talk", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncHomeRailExcludesTalkNewsAndReality()
    {
        var catalog = new NonScriptedMixRecordingOnTvThisWeekCatalog();
        var service = CreateService(catalog, new OnTvThisWeekFakeCache());

        var result = await service.GetOnTvThisWeekAsync(
            new OnTvThisWeekCriteria(1, 5, OnTvThisWeekPresentationIntent.HomeRail),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(result.Items);
        Assert.Equal("Scripted One", result.Items[0].Title);
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncHomeRailReturnsShorterSectionWhenTwoPagesAreInsufficient()
    {
        var catalog = new TalkHeavyTwoPageCatalog();
        var service = CreateService(catalog, new OnTvThisWeekFakeCache());

        var result = await service.GetOnTvThisWeekAsync(
            new OnTvThisWeekCriteria(1, 5, OnTvThisWeekPresentationIntent.HomeRail),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal([1, 2], catalog.RequestedPages);
        Assert.Single(result.Items);
        Assert.Equal("Only Scripted", result.Items[0].Title);
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncDiscoverBrowseExcludesNewsAndReality()
    {
        var catalog = new NonScriptedMixRecordingOnTvThisWeekCatalog();
        var service = CreateService(catalog, new OnTvThisWeekFakeCache());

        var result = await service.GetOnTvThisWeekAsync(
            new OnTvThisWeekCriteria(1, 5, OnTvThisWeekPresentationIntent.DiscoverBrowse),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(result.Items);
        Assert.Equal("Scripted One", result.Items[0].Title);
    }

    [Fact]
    public async Task GetOnTvThisWeekAsyncCacheDoesNotShareHomeRailAndDiscoverBrowseEntries()
    {
        var catalog = new NonScriptedMixRecordingOnTvThisWeekCatalog();
        var cache = new OnTvThisWeekFakeCache();
        var service = CreateService(catalog, cache);
        var discoverCriteria = new OnTvThisWeekCriteria(1, 2, OnTvThisWeekPresentationIntent.DiscoverBrowse);
        var homeCriteria = new OnTvThisWeekCriteria(1, 2, OnTvThisWeekPresentationIntent.HomeRail);
        var discoverCacheKey = OnTvThisWeekCacheKeys.Create(discoverCriteria, ContentLocaleResolver.EnglishUnitedStates);
        var homeCacheKey = OnTvThisWeekCacheKeys.Create(homeCriteria, ContentLocaleResolver.EnglishUnitedStates);

        Assert.NotEqual(discoverCacheKey, homeCacheKey);

        var discoverResult = await service.GetOnTvThisWeekAsync(discoverCriteria, ContentLocaleResolver.EnglishUnitedStates);
        var homeResult = await service.GetOnTvThisWeekAsync(homeCriteria, ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(discoverResult.Items);
        Assert.Single(homeResult.Items);
        Assert.Equal(2, catalog.CallCount);
    }

    private static OnTvThisWeekService CreateService(
        IOnTvThisWeekCatalog catalog,
        ICacheService cache) =>
        new(
            catalog,
            new FakeTvShowRepository(),
            cache,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            NullLogger<OnTvThisWeekService>.Instance);

    private static SearchItem CreateTvItem(Guid id, string title) =>
        new(id, "tv", title, null, null, null, null, null, 0m, 0, null);

    private sealed class TalkHeavyTwoPageCatalog : IOnTvThisWeekCatalog
    {
        public List<int> RequestedPages { get; } = [];

        public Task<TvShowProviderSearchResult> GetOnTheAirTvShowsAsync(
            int page,
            CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(page);
            IReadOnlyList<TvShowProviderSummary> items = page switch
            {
                1 => [TalkSummary(10, "Talk A"), TalkSummary(11, "Talk B")],
                2 => [TalkSummary(12, "Talk C"), ScriptedSummary(20, "Only Scripted")],
                _ => [],
            };

            return Task.FromResult(new TvShowProviderSearchResult(items, page, 20, 100, 5));
        }

        private static TvShowProviderSummary TalkSummary(int tmdbId, string title) =>
            ScriptedSummary(tmdbId, title) with { GenreTmdbIds = [OnTvThisWeekContentSelector.TalkTmdbGenreId] };

        private static TvShowProviderSummary ScriptedSummary(int tmdbId, string title) =>
            new(
                $"fake-{tmdbId}",
                tmdbId,
                null,
                null,
                title,
                title,
                "Overview",
                new DateOnly(2024, 1, 1),
                "/poster.jpg",
                null,
                "en",
                8m,
                100,
                GenreTmdbIds: [18]);
    }

    private sealed class NonScriptedMixRecordingOnTvThisWeekCatalog : IOnTvThisWeekCatalog
    {
        public int CallCount { get; private set; }

        public Task<TvShowProviderSearchResult> GetOnTheAirTvShowsAsync(
            int page,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new TvShowProviderSearchResult(
                [
                    ScriptedSummary(1, "Scripted One"),
                    new TvShowProviderSummary(
                        "fake-news",
                        2,
                        null,
                        null,
                        "Evening News",
                        "Evening News",
                        "Overview",
                        new DateOnly(2024, 1, 1),
                        "/poster.jpg",
                        null,
                        "en",
                        8m,
                        100,
                        GenreTmdbIds: [OnTvThisWeekContentSelector.NewsTmdbGenreId]),
                    ScriptedSummary(3, "Reality Show") with
                    {
                        GenreTmdbIds = [OnTvThisWeekContentSelector.RealityTmdbGenreId]
                    },
                ],
                page,
                20,
                3,
                1));
        }

        private static TvShowProviderSummary ScriptedSummary(int tmdbId, string title) =>
            new(
                $"fake-{tmdbId}",
                tmdbId,
                null,
                null,
                title,
                title,
                "Overview",
                new DateOnly(2024, 1, 1),
                "/poster.jpg",
                null,
                "en",
                8m,
                100,
                GenreTmdbIds: [18]);
    }

    private sealed class MultiPageRecordingOnTvThisWeekCatalog : IOnTvThisWeekCatalog
    {
        public List<int> RequestedPages { get; } = [];

        public Task<TvShowProviderSearchResult> GetOnTheAirTvShowsAsync(
            int page,
            CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(page);
            IReadOnlyList<TvShowProviderSummary> items = page switch
            {
                1 =>
                [
                    ScriptedSummary(1, "Scripted One"),
                    TalkSummary(100, "Talk One"),
                    TalkSummary(101, "Talk Two"),
                ],
                2 =>
                [
                    ScriptedSummary(2, "Scripted Two"),
                    ScriptedSummary(3, "Scripted Three"),
                ],
                _ => [],
            };

            return Task.FromResult(new TvShowProviderSearchResult(items, page, 20, 100, 5));
        }

        private static TvShowProviderSummary ScriptedSummary(int tmdbId, string title) =>
            new(
                $"fake-{tmdbId}",
                tmdbId,
                null,
                null,
                title,
                title,
                "Overview",
                new DateOnly(2024, 1, 1),
                "/poster.jpg",
                null,
                "en",
                8m,
                100,
                GenreTmdbIds: [18]);

        private static TvShowProviderSummary TalkSummary(int tmdbId, string title) =>
            ScriptedSummary(tmdbId, title) with { GenreTmdbIds = [OnTvThisWeekContentSelector.TalkTmdbGenreId] };
    }

    private sealed class RecordingOnTvThisWeekCatalog : IOnTvThisWeekCatalog
    {
        public int CallCount { get; private set; }

        public bool ShouldThrow { get; init; }

        public Task<TvShowProviderSearchResult> GetOnTheAirTvShowsAsync(
            int page,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (ShouldThrow)
            {
                throw new InvalidOperationException("catalog failed");
            }

            return Task.FromResult(new TvShowProviderSearchResult(
                [
                    new TvShowProviderSummary(
                        "fake-tmdb-1",
                        1,
                        null,
                        "tt1",
                        "Airing Drama",
                        "Airing Drama",
                        "Overview",
                        new DateOnly(2024, 1, 1),
                        "/poster.jpg",
                        null,
                        "en",
                        7m,
                        80,
                        GenreTmdbIds: [18]),
                    new TvShowProviderSummary(
                        "fake-tmdb-2",
                        2,
                        null,
                        "tt2",
                        "Late Night Talk",
                        "Late Night Talk",
                        "Overview",
                        new DateOnly(2024, 1, 1),
                        "/poster.jpg",
                        null,
                        "en",
                        7m,
                        80,
                        GenreTmdbIds: [OnTvThisWeekContentSelector.TalkTmdbGenreId]),
                ],
                page,
                20,
                1,
                1));
        }
    }

    private sealed class FakeTvShowRepository : ITvShowRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default)
        {
            var ids = summaries
                .Where(summary => summary.TmdbId is not null)
                .ToDictionary(
                    summary => summary.TmdbId!.Value,
                    summary => Guid.NewGuid());

            return Task.FromResult<IReadOnlyDictionary<int, Guid>>(ids);
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

    private sealed class OnTvThisWeekFakeCache : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

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
            _entries[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }
}
