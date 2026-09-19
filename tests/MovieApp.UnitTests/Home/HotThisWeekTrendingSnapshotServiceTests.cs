using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;

namespace MovieApp.UnitTests.Home;

public sealed class HotThisWeekTrendingSnapshotServiceTests
{
    private static readonly Guid MovieId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid TvId = Guid.Parse("22222222-2222-2222-2222-222222222201");

    [Fact]
    public async Task RefreshAsyncReplacesSnapshotWhenProviderReturnsUsableItems()
    {
        var cache = new TrackingCacheService();
        var provider = new RecordingTrendingWeekDataProvider(
        [
            CreateProviderItem("movie", 910001, "Trending Movie One"),
            CreateProviderItem("tv", 920001, "Trending Show One"),
        ]);
        var movies = new RecordingMovieRepository(new Dictionary<int, Guid> { [910001] = MovieId });
        var tvShows = new RecordingTvShowRepository(new Dictionary<int, Guid> { [920001] = TvId });
        var service = CreateService(cache, provider, movies, tvShows);

        cache.SetExistingSnapshot(CreateSnapshotEntry("Old Item"));

        var result = await service.RefreshAsync();

        Assert.True(result.SnapshotUpdated);
        Assert.Equal(2, result.MappedItemCount);
        var snapshot = await service.GetSnapshotAsync();
        Assert.NotNull(snapshot);
        Assert.Equal(
            ["Trending Movie One", "Trending Show One"],
            snapshot!.Items.Select(item => item.Title).ToList());
        Assert.True(snapshot.RefreshedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Equal(TimeSpan.FromDays(7), cache.LastSetExpiry);
        Assert.Equal(HotThisWeekTrendingSnapshotCacheKeys.Canonical, cache.LastSetKey);
    }

    [Fact]
    public async Task RefreshAsyncPreservesLastGoodSnapshotWhenProviderReturnsEmpty()
    {
        var cache = new TrackingCacheService();
        var existing = CreateSnapshotEntry("Existing Item");
        cache.SetExistingSnapshot(existing);
        var provider = new RecordingTrendingWeekDataProvider([]);
        var service = CreateService(cache, provider, new RecordingMovieRepository(), new RecordingTvShowRepository());

        var result = await service.RefreshAsync();

        Assert.False(result.SnapshotUpdated);
        var snapshot = await service.GetSnapshotAsync();
        Assert.Equal(existing.Items.Single().Title, snapshot!.Items.Single().Title);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task RefreshAsyncPreservesLastGoodSnapshotWhenProviderThrows()
    {
        var cache = new TrackingCacheService();
        var existing = CreateSnapshotEntry("Existing Item");
        cache.SetExistingSnapshot(existing);
        var provider = new RecordingTrendingWeekDataProvider([], shouldThrow: true);
        var service = CreateService(cache, provider, new RecordingMovieRepository(), new RecordingTvShowRepository());

        var result = await service.RefreshAsync();

        Assert.False(result.SnapshotUpdated);
        var snapshot = await service.GetSnapshotAsync();
        Assert.Equal(existing.Items.Single().Title, snapshot!.Items.Single().Title);
        Assert.Equal(1, cache.SetCount);
    }

    [Fact]
    public async Task RefreshAsyncPreservesLastGoodSnapshotWhenAllItemsAreUnmapped()
    {
        var cache = new TrackingCacheService();
        cache.SetExistingSnapshot(CreateSnapshotEntry("Existing Item"));
        var provider = new RecordingTrendingWeekDataProvider(
        [
            CreateProviderItem("movie", 910001, "Trending Movie One"),
        ]);
        var service = CreateService(
            cache,
            provider,
            new RecordingMovieRepository(),
            new RecordingTvShowRepository());

        var result = await service.RefreshAsync();

        Assert.False(result.SnapshotUpdated);
        Assert.Equal(1, result.SkippedItemCount);
    }

    private static HotThisWeekTrendingSnapshotService CreateService(
        ICacheService cache,
        ITrendingWeekDataProvider provider,
        IMovieRepository movieRepository,
        ITvShowRepository tvShowRepository) =>
        new(
            provider,
            movieRepository,
            tvShowRepository,
            cache,
            Options.Create(new HotThisWeekTrendingRefreshOptions { SnapshotTtlDays = 7 }),
            NullLogger<HotThisWeekTrendingSnapshotService>.Instance);

    private static HotThisWeekTrendingSnapshotEntry CreateSnapshotEntry(string title) =>
        new()
        {
            RefreshedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Items =
            [
                new SearchItem(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    "movie",
                    title,
                    null,
                    null,
                    "/poster.jpg",
                    null,
                    new DateOnly(2024, 1, 1),
                    8m,
                    100,
                    2024),
            ],
        };

    private static TrendingWeekProviderItem CreateProviderItem(string mediaType, int tmdbId, string title) =>
        new(
            mediaType,
            tmdbId,
            title,
            null,
            "Overview",
            new DateOnly(2025, 1, 1),
            "/poster.jpg",
            "/backdrop.jpg",
            8.1m,
            1200);

    private sealed class RecordingTrendingWeekDataProvider(
        IReadOnlyList<TrendingWeekProviderItem> items,
        bool shouldThrow = false) : ITrendingWeekDataProvider
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<TrendingWeekProviderItem>> GetTrendingWeekAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (shouldThrow)
            {
                throw new InvalidOperationException("TMDB unavailable");
            }

            return Task.FromResult(items);
        }
    }

    private sealed class RecordingMovieRepository : IMovieRepository
    {
        private readonly Dictionary<int, Guid> _ids;

        public RecordingMovieRepository()
        {
            _ids = new Dictionary<int, Guid>();
        }

        public RecordingMovieRepository(Dictionary<int, Guid> ids)
        {
            _ids = ids;
        }

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<MovieProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue && _ids.ContainsKey(summary.TmdbId.Value))
                    .ToDictionary(summary => summary.TmdbId!.Value, summary => _ids[summary.TmdbId!.Value]));

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
        private readonly Dictionary<int, Guid> _ids;

        public RecordingTvShowRepository()
        {
            _ids = new Dictionary<int, Guid>();
        }

        public RecordingTvShowRepository(Dictionary<int, Guid> ids)
        {
            _ids = ids;
        }

        public Task<IReadOnlyDictionary<int, Guid>> EnsureFromSummariesAsync(
            IReadOnlyList<TvShowProviderSummary> summaries,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(
                summaries
                    .Where(summary => summary.TmdbId.HasValue && _ids.ContainsKey(summary.TmdbId.Value))
                    .ToDictionary(summary => summary.TmdbId!.Value, summary => _ids[summary.TmdbId!.Value]));

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

        public string? LastSetKey { get; private set; }

        public TimeSpan? LastSetExpiry { get; private set; }

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
            LastSetKey = key;
            LastSetExpiry = expiry;
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
