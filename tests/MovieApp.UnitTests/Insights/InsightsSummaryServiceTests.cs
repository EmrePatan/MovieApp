using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;
namespace MovieApp.UnitTests.Insights;

public sealed class InsightsSummaryServiceTests
{
    [Fact]
    public async Task GetSummaryAsyncReturnsCachedResultWithoutRepositoryCall()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingInsightsRepository();
        var cache = new RecordingInsightsCacheService();
        var insightsCache = new InsightsCache(cache);
        var service = CreateService(userId, repository, insightsCache);

        var cached = CreateSummaryResult();
        await insightsCache.SetSummaryAsync(
            userId,
            "Europe/Istanbul",
            cached,
            TimeSpan.FromMinutes(5));

        var result = await service.GetSummaryAsync("Europe/Istanbul");

        Assert.Equal(cached, result);
        Assert.Equal(0, repository.CallCount);
    }

    [Fact]
    public async Task GetSummaryAsyncBuildsAndCachesOnMiss()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingInsightsRepository();
        var cache = new RecordingInsightsCacheService();
        var insightsCache = new InsightsCache(cache);
        var service = CreateService(userId, repository, insightsCache);

        var first = await service.GetSummaryAsync("Europe/Istanbul");
        var second = await service.GetSummaryAsync("Europe/Istanbul");

        Assert.Equal(1, repository.CallCount);
        Assert.Equal(first.MemberSince, second.MemberSince);
        Assert.Equal(2, first.Summary.MoviesWatched);
        Assert.Equal(1, first.Summary.ShowsStarted);
        Assert.Equal(1, first.Summary.RatingsCount);
        Assert.Equal(4.0m, first.Summary.AverageStarRating);
        Assert.Contains(cache.StoredSummaryKeys, key => key.Contains("europe/istanbul", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetSummaryAsyncReturnsAuthenticatedUserMemberSince()
    {
        var userId = Guid.NewGuid();
        var memberSince = new DateTime(2025, 3, 10, 8, 30, 0, DateTimeKind.Utc);
        var repository = new CountingInsightsRepository(memberSince);
        var service = CreateService(userId, repository, new InsightsCache(new RecordingInsightsCacheService()));

        var result = await service.GetSummaryAsync();

        Assert.Equal(memberSince, result.MemberSince);
    }

    [Fact]
    public async Task GetSummaryAsyncReturnsEmptyDnaForNewUser()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingInsightsRepository(
            moviesWatched: 0,
            showsStarted: 0,
            ratingsCount: 0);
        var service = CreateService(userId, repository, new InsightsCache(new RecordingInsightsCacheService()));

        var result = await service.GetSummaryAsync();

        Assert.Empty(result.MovieDna);
        Assert.Equal(0, result.Summary.MoviesWatched);
        Assert.Equal(0, result.Summary.ShowsStarted);
        Assert.Null(result.Summary.AverageStarRating);
    }

    [Fact]
    public async Task GetSummaryAsyncSupportsMovieOnlyUser()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingInsightsRepository(
            moviesWatched: 6,
            showsStarted: 0,
            movieTitles: CreateMovieOnlyTitles());
        var service = CreateService(userId, repository, new InsightsCache(new RecordingInsightsCacheService()));

        var result = await service.GetSummaryAsync();

        Assert.Equal(6, result.Summary.MoviesWatched);
        Assert.Equal(0, result.Summary.ShowsStarted);
        Assert.Equal(6, result.WatchingMix.MovieTitleCount);
        Assert.Equal(0, result.WatchingMix.SeriesTitleCount);
        Assert.Contains(result.MovieDna, label => label.Code == InsightsMovieDnaBuilder.MovieFirstCode);
    }

    [Fact]
    public async Task GetSummaryAsyncSupportsTvOnlyUser()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingInsightsRepository(
            moviesWatched: 0,
            showsStarted: 6,
            tvShowTitles: CreateTvOnlyTitles());
        var service = CreateService(userId, repository, new InsightsCache(new RecordingInsightsCacheService()));

        var result = await service.GetSummaryAsync();

        Assert.Equal(0, result.Summary.MoviesWatched);
        Assert.Equal(6, result.Summary.ShowsStarted);
        Assert.Contains(result.MovieDna, label => label.Code == InsightsMovieDnaBuilder.SeriesFirstCode);
    }

    [Fact]
    public async Task InvalidateForUserAsyncPreventsReturningPreviousCachedSummary()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingInsightsRepository();
        var cache = new RecordingInsightsCacheService();
        var insightsCache = new InsightsCache(cache);
        var invalidator = new UserAnalyticsCacheInvalidator(
            new ProfileStatisticsCache(cache),
            insightsCache,
            cache);
        var service = CreateService(userId, repository, insightsCache);

        await service.GetSummaryAsync("Europe/Istanbul");
        await invalidator.InvalidateForUserAsync(userId);
        await service.GetSummaryAsync("Europe/Istanbul");

        Assert.Equal(2, repository.CallCount);
    }

    private static InsightsSummaryService CreateService(
        Guid userId,
        IInsightsRepository repository,
        IInsightsCache insightsCache) =>
        new(
            new FakeCurrentUser(userId),
            repository,
            insightsCache,
            Options.Create(new InsightsOptions { SummaryCacheTtlMinutes = 5 }),
            NullLogger<InsightsSummaryService>.Instance);

    private static InsightsSummaryResult CreateSummaryResult() =>
        new(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            [],
            new InsightsSummaryStatsResult(1, 0, 0, 0, null),
            new InsightsWatchingMixResult(1, 0),
            DateTime.UtcNow);

    private static List<InsightsDnaTitleData> CreateMovieOnlyTitles() =>
        Enumerable.Range(0, 6)
            .Select(_ => new InsightsDnaTitleData(2026, [new InsightsDnaGenreData(Guid.NewGuid(), "Action")]))
            .ToList();

    private static List<InsightsDnaTitleData> CreateTvOnlyTitles() =>
        Enumerable.Range(0, 6)
            .Select(_ => new InsightsDnaTitleData(2026, [new InsightsDnaGenreData(Guid.NewGuid(), "Drama")]))
            .ToList();

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class CountingInsightsRepository : IInsightsRepository
    {
        private readonly DateTime _memberSince;
        private readonly int _moviesWatched;
        private readonly int _showsStarted;
        private readonly int _ratingsCount;
        private readonly IReadOnlyList<InsightsDnaTitleData> _movieTitles;
        private readonly IReadOnlyList<InsightsDnaTitleData> _tvShowTitles;

        public CountingInsightsRepository(
            DateTime? memberSince = null,
            int moviesWatched = 2,
            int showsStarted = 1,
            int ratingsCount = 1,
            IReadOnlyList<InsightsDnaTitleData>? movieTitles = null,
            IReadOnlyList<InsightsDnaTitleData>? tvShowTitles = null)
        {
            _memberSince = memberSince ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            _moviesWatched = moviesWatched;
            _showsStarted = showsStarted;
            _ratingsCount = ratingsCount;
            _movieTitles = movieTitles ??
            [
                new InsightsDnaTitleData(2026, [new InsightsDnaGenreData(Guid.NewGuid(), "Action")]),
                new InsightsDnaTitleData(2026, [new InsightsDnaGenreData(Guid.NewGuid(), "Action")]),
            ];
            _tvShowTitles = tvShowTitles ??
            [
                new InsightsDnaTitleData(2026, [new InsightsDnaGenreData(Guid.NewGuid(), "Drama")]),
            ];
        }

        public int CallCount { get; private set; }

        public Task<(InsightsSummaryRawData Raw, InsightsSummaryQueryMetrics Metrics)> GetSummaryRawDataAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var raw = new InsightsSummaryRawData(
                _memberSince,
                _moviesWatched,
                0,
                _showsStarted,
                _ratingsCount,
                [(8, _ratingsCount)],
                _movieTitles,
                _tvShowTitles);

            return Task.FromResult((raw, new InsightsSummaryQueryMetrics { DbRoundTrips = 4, DbTotalMs = 10 }));
        }

        public Task<(InsightsAnalyticsRawData Raw, InsightsAnalyticsQueryMetrics Metrics)> GetAnalyticsRawDataAsync(
            Guid userId,
            DateTime activityUtcStart,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(InsightsV3RawData Raw, InsightsV3QueryMetrics Metrics)> GetV3RawDataAsync(
            Guid userId,
            TimeZoneInfo timeZone,
            int year,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingInsightsCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public List<string> StoredSummaryKeys { get; } = [];

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
            _entries[key] = value;
            if (key.StartsWith($"{InsightsCacheKeys.Prefix}summary:", StringComparison.Ordinal))
            {
                StoredSummaryKeys.Add(key);
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }
}
