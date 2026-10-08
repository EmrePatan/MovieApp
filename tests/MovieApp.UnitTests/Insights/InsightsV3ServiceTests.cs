using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Configuration;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsV3ServiceTests
{
    private static readonly string TimeZoneId = OperatingSystem.IsWindows()
        ? "Turkey Standard Time"
        : "Europe/Istanbul";

    [Fact]
    public void DefaultAnalyticsCacheTtlKeepsAnUnchangedLibraryForTwelveHours()
    {
        Assert.Equal(12 * 60, new InsightsOptions().AnalyticsCacheTtlMinutes);
    }

    [Fact]
    public async Task GetInsightsV3AsyncReturnsCachedResultWithoutRepositoryCall()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingV3Repository();
        var cache = new RecordingInsightsCacheService();
        var insightsCache = new InsightsCache(cache);
        var service = CreateService(userId, repository, insightsCache);

        var cached = CreateV3Result();
        await insightsCache.SetV3Async(userId, TimeZoneId, cached.Meta.Year, cached, TimeSpan.FromMinutes(5));

        var result = await service.GetInsightsV3Async(TimeZoneId, cached.Meta.Year);

        Assert.Equal(cached.Meta.GeneratedAtUtc, result.Meta.GeneratedAtUtc);
        Assert.Equal(0, repository.CallCount);
    }

    [Fact]
    public async Task GetInsightsV3AsyncBuildsAndCachesOnMiss()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingV3Repository();
        var cache = new RecordingInsightsCacheService();
        var insightsCache = new InsightsCache(cache);
        var service = CreateService(userId, repository, insightsCache);

        var first = await service.GetInsightsV3Async(TimeZoneId, null);
        var second = await service.GetInsightsV3Async(TimeZoneId, null);

        Assert.Equal(1, repository.CallCount);
        Assert.Equal(first.Meta.Year, second.Meta.Year);
        Assert.Contains(cache.StoredV3Keys, key => key.Contains("v3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetInsightsV3AsyncRejectsInvalidTimezone()
    {
        var service = CreateService(
            Guid.NewGuid(),
            new CountingV3Repository(),
            new InsightsCache(new RecordingInsightsCacheService()));

        await Assert.ThrowsAsync<ValidationException>(() => service.GetInsightsV3Async("Invalid/Zone", null));
    }

    [Fact]
    public async Task GetInsightsV3AsyncUsesTimezoneInCacheKey()
    {
        var userId = Guid.NewGuid();
        var cache = new RecordingInsightsCacheService();
        var insightsCache = new InsightsCache(cache);
        var service = CreateService(userId, new CountingV3Repository(), insightsCache);

        await service.GetInsightsV3Async(TimeZoneId, null);
        await service.GetInsightsV3Async("UTC", null);

        Assert.Equal(2, cache.StoredV3Keys.Count);
        Assert.Contains(
            cache.StoredV3Keys,
            key => key.Contains("turkey standard time", StringComparison.Ordinal)
                || key.Contains("europe/istanbul", StringComparison.Ordinal));
        Assert.Contains(cache.StoredV3Keys, key => key.Contains("utc", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InvalidateForUserAsyncPreventsReturningPreviousCachedV3()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingV3Repository();
        var cache = new RecordingInsightsCacheService();
        var insightsCache = new InsightsCache(cache);
        var invalidator = new UserAnalyticsCacheInvalidator(
            new ProfileStatisticsCache(cache),
            insightsCache,
            cache);
        var service = CreateService(userId, repository, insightsCache);

        await service.GetInsightsV3Async(TimeZoneId, null);
        await invalidator.InvalidateForUserAsync(userId);
        await service.GetInsightsV3Async(TimeZoneId, null);

        Assert.Equal(2, repository.CallCount);
    }

    [Fact]
    public async Task ConcurrentMissesShareOneRepositoryBuild()
    {
        var userId = Guid.NewGuid();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new GatedV3Repository(entered, release);
        var service = CreateService(userId, repository, new InsightsCache(new RecordingInsightsCacheService()));

        var first = service.GetInsightsV3Async(TimeZoneId, null);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = service.GetInsightsV3Async(TimeZoneId, null);
        release.TrySetResult();

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, repository.CallCount);
        Assert.Equal(results[0].Meta.GeneratedAtUtc, results[1].Meta.GeneratedAtUtc);
    }

    [Fact]
    public async Task BuildSkipsCacheWriteWhenGenerationChangesDuringTheMiss()
    {
        var userId = Guid.NewGuid();
        var cache = new BumpGenerationDuringBuildCache();
        var insightsCache = new InsightsCache(cache);
        var repository = new CountingV3Repository();
        var service = CreateService(userId, repository, insightsCache);

        await service.GetInsightsV3Async(TimeZoneId, null);

        Assert.Equal(1, repository.CallCount);
        Assert.DoesNotContain(cache.WrittenKeys, key => key.Contains("v3", StringComparison.Ordinal));

        await service.GetInsightsV3Async(TimeZoneId, null);

        Assert.Equal(2, repository.CallCount);
    }

    private static InsightsV3Service CreateService(
        Guid userId,
        IInsightsRepository repository,
        IInsightsCache insightsCache) =>
        new(
            new FakeCurrentUser(userId),
            repository,
            insightsCache,
            Options.Create(new InsightsOptions { AnalyticsCacheTtlMinutes = 5 }),
            Options.Create(new InsightsV3Options()),
            new InsightsV3LoadCoordinator(),
            NullLogger<InsightsV3Service>.Instance);

    private static InsightsV3Result CreateV3Result()
    {
        var generatedAt = DateTime.UtcNow;
        var emptyMix = new InsightsV3WatchingMixResult(0, 0, 0m, 0m);
        return new InsightsV3Result(
            new InsightsV3MetaResult(generatedAt, generatedAt, TimeZoneId, generatedAt.Year),
            new InsightsV3MovieDnaResult(string.Empty, [], [], [], emptyMix),
            new InsightsV3YourYearResult([], 0, null, null),
            new InsightsV3TasteSectionResult([], null),
            new InsightsV3TimeInStoriesResult(0, 0, 0, 0, 0m),
            new InsightsV3RatingsSectionResult(0, null, [], null, null),
            new InsightsV3EraSectionResult([], null, 0, null),
            new InsightsV3RecordsSectionResult(null, null, null, null),
            []);
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private class CountingV3Repository : IInsightsRepository
    {
        public int CallCount { get; private set; }

        public virtual Task<(InsightsV3RawData Raw, InsightsV3QueryMetrics Metrics)> GetV3RawDataAsync(
            Guid userId,
            TimeZoneInfo timeZone,
            int year,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var milestoneRaw = new InsightsAnalyticsRawData(
                new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                0,
                0,
                0,
                0,
                [],
                [],
                [],
                0,
                0,
                0,
                0,
                [],
                [],
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);

            var raw = new InsightsV3RawData(
                milestoneRaw.MemberSinceUtc,
                0,
                0,
                0,
                0,
                0,
                0,
                [],
                0,
                [],
                0,
                [],
                0,
                [],
                0,
                new InsightsV3YearActivityAggregate([], new Dictionary<DayOfWeek, int>(), 0, 0),
                new InsightsV3RecordsRawData(null, null, null),
                0,
                0,
                0,
                0,
                [],
                [],
                null,
                milestoneRaw);

            return Task.FromResult((raw, new InsightsV3QueryMetrics { DbRoundTrips = 11, DbTotalMs = 20 }));
        }
    }

    private sealed class GatedV3Repository(TaskCompletionSource entered, TaskCompletionSource release)
        : CountingV3Repository
    {
        public override async Task<(InsightsV3RawData Raw, InsightsV3QueryMetrics Metrics)> GetV3RawDataAsync(
            Guid userId,
            TimeZoneInfo timeZone,
            int year,
            CancellationToken cancellationToken = default)
        {
            entered.TrySetResult();
            await release.Task;
            return await base.GetV3RawDataAsync(userId, timeZone, year, cancellationToken);
        }
    }

    private sealed class BumpGenerationDuringBuildCache : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();
        private int _generationReads;

        public List<string> WrittenKeys { get; } = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class
        {
            if (key.Contains(":gen:", StringComparison.Ordinal))
            {
                _generationReads++;
                var generation = _generationReads >= 5 ? 1L : 0L;
                return Task.FromResult((T?)(object)new InsightsGenerationState(generation));
            }

            return Task.FromResult(_entries.TryGetValue(key, out var value) ? value as T : null);
        }

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value!;
            WrittenKeys.Add(key);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _entries.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingInsightsCacheService : ICacheService
    {
        private readonly Dictionary<string, object> _entries = new();

        public List<string> StoredV3Keys { get; } = [];

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
            if (key.Contains("v3", StringComparison.Ordinal))
            {
                StoredV3Keys.Add(key);
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
