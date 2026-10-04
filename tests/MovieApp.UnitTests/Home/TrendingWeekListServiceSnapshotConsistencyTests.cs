using System.Globalization;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Home;

public sealed class TrendingWeekListServiceSnapshotConsistencyTests
{
    private static readonly DateOnly Today = new(2026, 10, 4);

    [Fact]
    public async Task GetPageAsyncPrefersSnapshotAndDoesNotCallProvider()
    {
        var weekly = CreateWeeklyItems(6);
        var provider = new RecordingTrendingWeekDataProvider([], shouldThrow: true);
        var service = CreateListService(new FakeSnapshotService(weekly), provider, new NoOpCacheService());

        var page = await service.GetPageAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, 20),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(0, provider.CallCount);
        Assert.Equal(
            weekly.Select(item => item.Title).ToList(),
            page.Items.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task GetPageAsyncPaginatesSnapshotInCanonicalOrder()
    {
        var weekly = CreateWeeklyItems(5);
        var service = CreateListService(new FakeSnapshotService(weekly), new RecordingTrendingWeekDataProvider([]), new NoOpCacheService());

        var page = await service.GetPageAsync(
            new DiscoveryCriteria(SearchContentType.All, 2, 2),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(["Weekly 3", "Weekly 4"], page.Items.Select(item => item.Title).ToList());
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
    }

    [Fact]
    public async Task GetPageAsyncExcludesFutureReleasesFromSnapshot()
    {
        List<SearchItem> weekly =
        [
            Item("Released", Today.AddDays(-1)),
            Item("Future", Today.AddDays(10)),
        ];
        var service = CreateListService(new FakeSnapshotService(weekly), new RecordingTrendingWeekDataProvider([]), new NoOpCacheService());

        var page = await service.GetPageAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, 10),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Single(page.Items);
        Assert.Equal("Released", page.Items[0].Title);
    }

    [Fact]
    public async Task GetPageAsyncFallsBackToProviderWhenSnapshotMissing()
    {
        var providerItems = CreateWeeklyItems(3);
        var provider = new RecordingTrendingWeekDataProvider(providerItems);
        var service = CreateListService(new FakeSnapshotService(null), provider, new NoOpCacheService());

        var page = await service.GetPageAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, 10),
            ContentLocaleResolver.EnglishUnitedStates);

        Assert.Equal(1, provider.CallCount);
        Assert.Equal(
            providerItems.Select(item => item.Title).ToList(),
            page.Items.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task HomeTrendingRailAndSeeAllPreserveRelativeOrderForSharedTitles()
    {
        List<SearchItem> weekly =
        [
            Item("Hero A", Today.AddDays(-5)),
            Item("Hero B", Today.AddDays(-4)),
            Item("Obsession", Today.AddDays(-30)),
            Item("Ranma1/2", Today.AddDays(-400), type: "tv", tmdbId: 259140),
            Item("Tail", Today.AddDays(-2)),
        ];

        var snapshot = new FakeSnapshotService(weekly);
        var listService = CreateListService(snapshot, new RecordingTrendingWeekDataProvider([]), new NoOpCacheService());
        var trendingSection = new HomeWeeklyTrendingSectionService(
            snapshot,
            new RecordingTrendingWeekListService([]),
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService());

        var heroItems = weekly.Take(2).ToList();
        var rail = await trendingSection.GetTrendingItemsAsync(
            SearchContentType.All,
            heroItems,
            heroSize: 2,
            trendingSize: 4,
            ContentLocaleResolver.EnglishUnitedStates);
        var seeAll = await listService.GetPageAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, 10),
            ContentLocaleResolver.EnglishUnitedStates);

        var railPair = rail
            .Where(item => item.Title is "Obsession" or "Ranma1/2")
            .Select(item => item.Title)
            .ToList();
        var seeAllPair = seeAll.Items
            .Where(item => item.Title is "Obsession" or "Ranma1/2")
            .Select(item => item.Title)
            .ToList();

        Assert.Equal(["Obsession", "Ranma1/2"], railPair);
        Assert.Equal(["Obsession", "Ranma1/2"], seeAllPair);
        Assert.Contains(seeAll.Items, item => item.Title == "Hero A");
        Assert.DoesNotContain(rail, item => item.Title == "Hero A");
    }

    [Fact]
    public async Task GetPageAsyncUsesDistinctCacheKeysWhenSnapshotGenerationChanges()
    {
        var locale = ContentLocaleResolver.EnglishUnitedStates;
        var criteria = new DiscoveryCriteria(SearchContentType.All, 1, 10);
        var oldGeneration = DateTimeOffset.Parse("2026-10-01T00:00:00Z", CultureInfo.InvariantCulture);
        var newGeneration = DateTimeOffset.Parse("2026-10-04T01:00:00Z", CultureInfo.InvariantCulture);

        var cache = new TrackingCacheService();
        var snapshot = new MutableSnapshotService(Entry(oldGeneration, [Item("Stale", Today)]));
        var service = CreateListService(snapshot, new RecordingTrendingWeekDataProvider([]), cache);

        _ = await service.GetPageAsync(criteria, locale);
        var staleKey = DiscoveryTrendingCacheKeys.CreateWeekList(criteria, oldGeneration.UtcTicks);
        Assert.True(cache.ContainsKey(staleKey));

        snapshot.Set(Entry(newGeneration, [Item("Fresh", Today)]));
        var freshPage = await service.GetPageAsync(criteria, locale);

        Assert.Equal("Fresh", freshPage.Items.Single().Title);
        var freshKey = DiscoveryTrendingCacheKeys.CreateWeekList(criteria, newGeneration.UtcTicks);
        Assert.True(cache.ContainsKey(freshKey));
        Assert.NotEqual(staleKey, freshKey);
    }

    [Fact]
    public void DiscoverBrowseCacheKeyIncludesSnapshotGenerationForTrendingMode()
    {
        var criteria = new DiscoverBrowseCriteria(
            DiscoverBrowseMode.Trending,
            SearchContentType.All,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            null,
            1,
            20);
        const long generation = 638956704000000000;

        var key = DiscoveryBrowseCacheKeys.Create(
            criteria,
            ContentLocaleResolver.EnglishUnitedStates,
            generation);

        Assert.Contains($":snap-{generation}", key, StringComparison.Ordinal);
    }

    private static TrendingWeekListService CreateListService(
        IHotThisWeekTrendingSnapshotService snapshotService,
        ITrendingWeekDataProvider provider,
        ICacheService cache) =>
        new(
            provider,
            snapshotService,
            new NoOpMovieRepository(),
            new NoOpTvShowRepository(),
            cache,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService());

    private static List<SearchItem> CreateWeeklyItems(int count) =>
        Enumerable.Range(1, count)
            .Select(index => Item($"Weekly {index}", Today.AddDays(-index)))
            .ToList();

    private static SearchItem Item(
        string title,
        DateOnly releaseDate,
        string type = "movie",
        int? tmdbId = null) =>
        new(
            Guid.NewGuid(),
            type,
            title,
            null,
            null,
            "/poster.jpg",
            null,
            releaseDate,
            7m,
            100,
            releaseDate.Year,
            tmdbId ?? title.GetHashCode());

    private static HotThisWeekTrendingSnapshotEntry Entry(DateTimeOffset refreshedAt, IReadOnlyList<SearchItem> items) =>
        new() { RefreshedAt = refreshedAt, Items = items };

    private sealed class FakeSnapshotService : IHotThisWeekTrendingSnapshotService
    {
        private readonly HotThisWeekTrendingSnapshotEntry? _entry;

        public FakeSnapshotService(IReadOnlyList<SearchItem>? items, DateTimeOffset? refreshedAt = null)
        {
            if (items is not null)
            {
                _entry = new HotThisWeekTrendingSnapshotEntry
                {
                    RefreshedAt = refreshedAt ?? DateTimeOffset.UtcNow,
                    Items = items,
                };
            }
        }

        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_entry);

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class MutableSnapshotService : IHotThisWeekTrendingSnapshotService
    {
        private HotThisWeekTrendingSnapshotEntry? _entry;

        public MutableSnapshotService(HotThisWeekTrendingSnapshotEntry entry) => _entry = entry;

        public void Set(HotThisWeekTrendingSnapshotEntry entry) => _entry = entry;

        public Task<HotThisWeekTrendingSnapshotEntry?> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_entry);

        public Task<HotThisWeekTrendingSnapshotRefreshResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingTrendingWeekListService(IReadOnlyList<SearchItem> items) : ITrendingWeekListService
    {
        public Task<PaginatedResult<SearchItem>> GetPageAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>(
                items.Take(criteria.PageSize).ToList(),
                criteria.Page,
                criteria.PageSize,
                items.Count,
                1));
    }

    private sealed class RecordingTrendingWeekDataProvider : ITrendingWeekDataProvider
    {
        private readonly IReadOnlyList<SearchItem> _items;
        private readonly bool _shouldThrow;

        public RecordingTrendingWeekDataProvider(IReadOnlyList<SearchItem> items, bool shouldThrow = false)
        {
            _items = items;
            _shouldThrow = shouldThrow;
        }

        public int CallCount { get; private set; }

        public Task<IReadOnlyList<TrendingWeekProviderItem>> GetTrendingWeekAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TrendingWeekPage> GetTrendingWeekPageAsync(int page, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_shouldThrow)
            {
                throw new HttpRequestException("provider down");
            }

            var providerItems = _items.Select(item => new TrendingWeekProviderItem(
                item.Type,
                item.TmdbId ?? 1,
                item.Title,
                item.OriginalTitle,
                item.Overview,
                item.ReleaseDate,
                item.PosterUrl,
                item.BackdropUrl,
                item.VoteAverage,
                item.VoteCount)).ToList();

            return Task.FromResult(new TrendingWeekPage(providerItems, page, providerItems.Count, 1));
        }
    }

    private sealed class TrackingCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string> Keys => _entries.Keys;

        public bool ContainsKey(string key) => _entries.ContainsKey(key);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            if (_entries.TryGetValue(key, out var value) && value is T typed)
            {
                return Task.FromResult<T?>(typed);
            }

            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
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

    private sealed class NoOpMovieRepository : MovieApp.Application.Abstractions.Persistence.IMovieRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries.ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid()));

        public Task<MovieApp.Domain.Entities.Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpTvShowRepository : MovieApp.Application.Abstractions.Persistence.ITvShowRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries.ToDictionary(summary => summary.TmdbId!.Value, _ => Guid.NewGuid()));

        public Task<MovieApp.Domain.Entities.TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpCacheService : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
            Task.FromResult<T?>(null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
