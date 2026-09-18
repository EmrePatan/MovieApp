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

public sealed class InsightsAnalyticsServiceTests
{
    private static readonly string TimeZoneId = OperatingSystem.IsWindows()
        ? "Turkey Standard Time"
        : "Europe/Istanbul";

    [Fact]
    public async Task GetAnalyticsAsyncReturnsCachedResultWithoutRepositoryCall()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingAnalyticsRepository();
        var cache = new RecordingInsightsCacheService();
        var insightsCache = new InsightsCache(cache);
        var service = CreateService(userId, repository, insightsCache);

        var cached = CreateAnalyticsResult();
        await insightsCache.SetAnalyticsAsync(userId, TimeZoneId, cached, TimeSpan.FromMinutes(5));

        var result = await service.GetAnalyticsAsync(TimeZoneId);

        Assert.Equal(cached.GeneratedAtUtc, result.GeneratedAtUtc);
        Assert.Equal(0, repository.CallCount);
    }

    [Fact]
    public async Task GetAnalyticsAsyncBuildsAndCachesOnMiss()
    {
        var userId = Guid.NewGuid();
        var repository = new CountingAnalyticsRepository();
        var cache = new RecordingInsightsCacheService();
        var insightsCache = new InsightsCache(cache);
        var service = CreateService(userId, repository, insightsCache);

        var first = await service.GetAnalyticsAsync(TimeZoneId);
        var second = await service.GetAnalyticsAsync(TimeZoneId);

        Assert.Equal(1, repository.CallCount);
        Assert.Equal(first.Activity.Days.Count, second.Activity.Days.Count);
        Assert.Contains(cache.StoredAnalyticsKeys, key => key.Contains("analytics", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetAnalyticsAsyncRejectsInvalidTimezone()
    {
        var service = CreateService(Guid.NewGuid(), new CountingAnalyticsRepository(), new InsightsCache(new RecordingInsightsCacheService()));

        await Assert.ThrowsAsync<ValidationException>(() => service.GetAnalyticsAsync("Invalid/Zone"));
    }

    [Fact]
    public async Task GetAnalyticsAsyncUsesTimezoneInCacheKey()
    {
        var userId = Guid.NewGuid();
        var cache = new RecordingInsightsCacheService();
        var insightsCache = new InsightsCache(cache);
        var service = CreateService(userId, new CountingAnalyticsRepository(), insightsCache);

        await service.GetAnalyticsAsync(TimeZoneId);
        await service.GetAnalyticsAsync("UTC");

        Assert.Equal(2, cache.StoredAnalyticsKeys.Count);
        Assert.Contains(cache.StoredAnalyticsKeys, key => key.Contains("turkey standard time", StringComparison.Ordinal) || key.Contains("europe/istanbul", StringComparison.Ordinal));
        Assert.Contains(cache.StoredAnalyticsKeys, key => key.Contains("utc", StringComparison.Ordinal));
    }

    private static InsightsAnalyticsService CreateService(
        Guid userId,
        IInsightsRepository repository,
        IInsightsCache insightsCache) =>
        new(
            new FakeCurrentUser(userId),
            repository,
            insightsCache,
            Options.Create(new InsightsOptions { AnalyticsCacheTtlMinutes = 5 }),
            NullLogger<InsightsAnalyticsService>.Instance);

    private static InsightsAnalyticsResult CreateAnalyticsResult() =>
        new(
            new InsightsActivityResult([], new InsightsActivitySummaryResult(0, null, null, 0, 0)),
            new InsightsTasteResult([]),
            new InsightsErasResult([], 0),
            new InsightsEstimatedTimeWatchedResult(0, 0, 0, 0, 0, 0m, null),
            new InsightsRatingsAnalyticsResult(0, null, [], null),
            [],
            DateTime.UtcNow);

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class CountingAnalyticsRepository : IInsightsRepository
    {
        public int CallCount { get; private set; }

        public Task<(InsightsSummaryRawData Raw, InsightsSummaryQueryMetrics Metrics)> GetSummaryRawDataAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<(InsightsAnalyticsRawData Raw, InsightsAnalyticsQueryMetrics Metrics)> GetAnalyticsRawDataAsync(
            Guid userId,
            DateTime activityUtcStart,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var raw = new InsightsAnalyticsRawData(
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

            return Task.FromResult((raw, new InsightsAnalyticsQueryMetrics { DbRoundTrips = 8, DbTotalMs = 20 }));
        }

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

        public List<string> StoredAnalyticsKeys { get; } = [];

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
            if (key.Contains("analytics", StringComparison.Ordinal))
            {
                StoredAnalyticsKeys.Add(key);
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
