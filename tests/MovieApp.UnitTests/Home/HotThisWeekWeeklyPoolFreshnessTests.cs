using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.UnitTests.Persistence;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.UnitTests.Search;

namespace MovieApp.UnitTests.Home;

public sealed class HotThisWeekWeeklyPoolFreshnessTests
{
    private static readonly DateOnly Today = new(2026, 10, 4);
    private static readonly Guid MovieId = Guid.Parse("11111111-1111-1111-1111-111111111101");

    [Fact]
    public async Task GetItemsAsyncReflectsSnapshotMembershipImmediatelyAfterRefresh()
    {
        var refreshedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var snapshot = new MutableSnapshotService(
            Entry(refreshedAt, [Item("Stale Title", Today.AddDays(-1))]));
        var cache = new TrackingCacheService();
        var service = CreateHotThisWeekService(cache, snapshot);

        var staleItems = await service.GetItemsAsync(SearchContentType.All, 5, ContentLocaleResolver.EnglishUnitedStates);
        Assert.Equal(["Stale Title"], staleItems.Select(item => item.Title).ToList());

        var newRefreshedAt = DateTimeOffset.UtcNow;
        snapshot.Set(Entry(newRefreshedAt, [Item("Fresh Title", Today.AddDays(-1))]));

        var freshItems = await service.GetItemsAsync(SearchContentType.All, 5, ContentLocaleResolver.EnglishUnitedStates);
        Assert.Equal(["Fresh Title"], freshItems.Select(item => item.Title).ToList());
    }

    [Fact]
    public async Task GetItemsAsyncDoesNotReuseWeeklyPoolFromPreviousSnapshotGeneration()
    {
        var locale = ContentLocaleResolver.EnglishUnitedStates;
        var oldGeneration = DateTimeOffset.Parse("2026-10-01T00:00:00Z", CultureInfo.InvariantCulture);
        var newGeneration = DateTimeOffset.Parse("2026-10-04T01:00:00Z", CultureInfo.InvariantCulture);

        var removedItem = Item("Ranma1/2", Today.AddDays(-30), tmdbId: 259140);
        var keptItem = Item("Kept Title", Today.AddDays(-1));

        var snapshot = new MutableSnapshotService(Entry(oldGeneration, [removedItem, keptItem]));
        var cache = new TrackingCacheService();
        var service = CreateHotThisWeekService(cache, snapshot);

        var beforeRefresh = await service.GetItemsAsync(SearchContentType.All, 5, locale);
        Assert.Equal(["Ranma1/2", "Kept Title"], beforeRefresh.Select(item => item.Title).ToList());

        snapshot.Set(Entry(newGeneration, [keptItem]));

        var afterRefresh = await service.GetItemsAsync(SearchContentType.All, 5, locale);
        Assert.Equal(["Kept Title"], afterRefresh.Select(item => item.Title).ToList());
        Assert.DoesNotContain(afterRefresh, item => item.TmdbId == 259140);
    }

    [Fact]
    public async Task RefreshAsyncPreservesLastGoodSnapshotWhenProviderFails()
    {
        var cache = new TrackingCacheService();
        var existing = Entry(DateTimeOffset.UtcNow.AddDays(-1), [Item("Existing Item", Today)]);
        cache.SetExistingSnapshot(existing);
        var provider = new RecordingTrendingWeekDataProvider([], shouldThrow: true);
        var snapshotService = CreateSnapshotService(cache, provider);

        var result = await snapshotService.RefreshAsync();

        Assert.False(result.SnapshotUpdated);
        var snapshot = await snapshotService.GetSnapshotAsync();
        Assert.Equal("Existing Item", snapshot!.Items.Single().Title);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task GetItemsAsyncStillExcludesFutureReleasesAfterSnapshotRefresh()
    {
        var snapshot = new MutableSnapshotService(
            Entry(DateTimeOffset.UtcNow.AddHours(-1), [
                Item("Released", Today.AddDays(-1)),
                Item("Future", Today.AddDays(10)),
            ]));
        var service = CreateHotThisWeekService(new TrackingCacheService(), snapshot);

        var items = await service.GetItemsAsync(SearchContentType.All, 5, ContentLocaleResolver.EnglishUnitedStates);
        Assert.Equal(["Released"], items.Select(item => item.Title).ToList());

        snapshot.Set(Entry(DateTimeOffset.UtcNow, [
            Item("Released", Today.AddDays(-1)),
            Item("Future", Today.AddDays(10)),
            Item("Also Released", Today),
        ]));

        items = await service.GetItemsAsync(SearchContentType.All, 5, ContentLocaleResolver.EnglishUnitedStates);
        Assert.Equal(["Released", "Also Released"], items.Select(item => item.Title).ToList());
    }

    private static HotThisWeekService CreateHotThisWeekService(
        ICacheService cache,
        MutableSnapshotService snapshot) =>
        new(
            new NoOpTrendingWeekListService(),
            snapshot,
            new SearchTestDoubles.PassthroughSummaryLocalizationOverlayService(),
            cache,
            new HotThisWeekLoadCoordinator(),
            Options.Create(new HomeOptions { HotThisWeekCacheTtlMinutes = 30 }),
            NullLogger<HotThisWeekService>.Instance);

    private static HotThisWeekTrendingSnapshotService CreateSnapshotService(
        ICacheService cache,
        ITrendingWeekDataProvider provider) =>
        new(
            provider,
            new RecordingMovieRepository(new Dictionary<int, Guid> { [910001] = MovieId }),
            new RecordingTvShowRepository(),
            new NoOpCatalogMetadataFreshnessRepository(),
            cache,
            Options.Create(new HotThisWeekTrendingRefreshOptions { SnapshotTtlDays = 7 }),
            NullLogger<HotThisWeekTrendingSnapshotService>.Instance);

    private static HotThisWeekTrendingSnapshotEntry Entry(
        DateTimeOffset refreshedAt,
        IReadOnlyList<SearchItem> items) =>
        new() { RefreshedAt = refreshedAt, Items = items };

    private static SearchItem Item(string title, DateOnly releaseDate, int? tmdbId = null) =>
        new(
            Guid.NewGuid(),
            "movie",
            title,
            null,
            null,
            "/poster.jpg",
            null,
            releaseDate,
            8m,
            100,
            releaseDate.Year,
            tmdbId);

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

    private sealed class NoOpTrendingWeekListService : ITrendingWeekListService
    {
        public Task<PaginatedResult<SearchItem>> GetPageAsync(
            DiscoveryCriteria criteria,
            string contentLocale,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PaginatedResult<SearchItem>([], criteria.Page, criteria.PageSize, 0, 0));
    }

    private sealed class RecordingTrendingWeekDataProvider(
        IReadOnlyList<TrendingWeekProviderItem> items,
        bool shouldThrow = false) : ITrendingWeekDataProvider
    {
        public Task<IReadOnlyList<TrendingWeekProviderItem>> GetTrendingWeekAsync(
            CancellationToken cancellationToken = default)
        {
            if (shouldThrow)
            {
                throw new InvalidOperationException("TMDB unavailable");
            }

            return Task.FromResult(items);
        }

        public Task<TrendingWeekPage> GetTrendingWeekPageAsync(int page, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingMovieRepository(Dictionary<int, Guid> ids) : IMovieRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue && ids.ContainsKey(summary.TmdbId.Value))
                    .ToDictionary(summary => summary.TmdbId!.Value, summary => ids[summary.TmdbId!.Value]));

        public Task<MovieApp.Domain.Entities.Movie?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.Movie> UpsertFromProviderAsync(
            MovieProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingTvShowRepository : ITvShowRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());

        public Task<MovieApp.Domain.Entities.TvShow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow?> GetByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MovieApp.Domain.Entities.TvShow> UpsertFromProviderAsync(
            TvShowProviderDetails details,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TrackingCacheService : ICacheService
    {
        public int SetCount { get; private set; }

        private readonly Dictionary<string, object> _entries = new();

        public void SetExistingSnapshot(HotThisWeekTrendingSnapshotEntry entry)
        {
            _entries[HotThisWeekTrendingSnapshotCacheKeys.Canonical] = entry;
            SetCount = 1;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default) where T : class
        {
            SetCount++;
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
