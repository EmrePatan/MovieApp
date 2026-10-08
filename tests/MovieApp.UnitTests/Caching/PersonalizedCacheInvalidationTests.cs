using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Abstractions.Identity;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Identity;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Services.Watchlists;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.Caching;

public sealed class PersonalizedCacheInvalidationTests
{
    [Fact]
    public async Task RecommendationBumpDoesNotReturnUntilTheGenerationIsStored()
    {
        var userId = Guid.NewGuid();
        var cache = new BlockingSetCache();
        var bump = BackgroundAnalyticsInvalidation.InvalidateRecommendationsAsync(
            cache,
            scopeFactory: null,
            userId,
            CancellationToken.None);

        await cache.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(bump.IsCompleted);

        cache.Release.TrySetResult();
        await bump.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, await new UserRecommendationCacheGeneration(cache).GetAsync(userId));
    }

    [Fact]
    public async Task RunAsyncBumpsBothGenerationsAndSchedulesOneRebuildPerCall()
    {
        var userId = Guid.NewGuid();
        var cache = new MemoryCache();
        var scheduler = new CountingScheduler();
        var services = new ServiceCollection();
        services.AddSingleton<ICacheService>(cache);
        services.AddSingleton<IPersonalizedCacheRebuildScheduler>(scheduler);
        await using var provider = services.BuildServiceProvider();

        for (var i = 0; i < 20; i++)
        {
            await BackgroundAnalyticsInvalidation.RunAsync(
                new NoOpInvalidator(),
                provider.GetRequiredService<IServiceScopeFactory>(),
                userId,
                CancellationToken.None);
        }

        Assert.Equal(20, await new UserRecommendationCacheGeneration(cache).GetAsync(userId));
        Assert.Equal(20, await new InsightsCache(cache).GetGenerationAsync(userId));
        Assert.Equal(20, scheduler.Schedules);
    }

    [Fact]
    public async Task RatingsInvalidatorBumpsBothGenerationsBeforeSchedulingTheRebuild()
    {
        var userId = Guid.NewGuid();
        var cache = new MemoryCache();
        var scheduler = new CountingScheduler();
        var invalidator = new UserAnalyticsCacheInvalidator(
            new NoOpProfileCache(),
            new InsightsCache(cache),
            cache,
            scheduler);

        await invalidator.InvalidateForUserAsync(userId);

        Assert.Equal(1, await new UserRecommendationCacheGeneration(cache).GetAsync(userId));
        Assert.Equal(1, await new InsightsCache(cache).GetGenerationAsync(userId));
        Assert.Equal(1, scheduler.Schedules);
    }

    [Fact]
    public async Task DeleteWatchlistBumpsTheRecommendationGenerationBeforeReturning()
    {
        var userId = Guid.NewGuid();
        var cache = new MemoryCache();
        var service = new DeleteWatchlistService(
            new StubCurrentUser(userId),
            new DeletingWatchlistRepository(),
            cache);

        await service.DeleteAsync(Guid.NewGuid());

        Assert.Equal(1, await new UserRecommendationCacheGeneration(cache).GetAsync(userId));
        Assert.Equal(0, await new InsightsCache(cache).GetGenerationAsync(userId));
    }

    [Fact]
    public async Task ClearingSearchHistoryBumpsTheRecommendationGenerationBeforeReturning()
    {
        var userId = Guid.NewGuid();
        var cache = new MemoryCache();
        var service = new SearchHistoryService(
            new StubCurrentUser(userId),
            new SearchHistoryRepositoryStub(),
            cache);

        await service.ClearHistoryAsync();

        Assert.Equal(1, await new UserRecommendationCacheGeneration(cache).GetAsync(userId));
        Assert.Equal(0, await new InsightsCache(cache).GetGenerationAsync(userId));
    }

    private sealed class CountingScheduler : IPersonalizedCacheRebuildScheduler
    {
        public int Schedules { get; private set; }

        public void Schedule(Guid userId) => Schedules++;
    }

    private sealed class NoOpProfileCache : IProfileStatisticsCache
    {
        public Task<UserStatisticsResult?> GetAsync(
            Guid userId,
            string? timeZoneId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<UserStatisticsResult?>(null);

        public Task SetAsync(
            Guid userId,
            string? timeZoneId,
            UserStatisticsResult statistics,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpInvalidator : IUserAnalyticsCacheInvalidator
    {
        public Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid? UserId => userId;
    }

    private sealed class DeletingWatchlistRepository : IWatchlistRepository
    {
        public Task<bool> DeleteAsync(Guid userId, Guid watchlistId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Watchlist?> GetByIdForUserAsync(Guid userId, Guid watchlistId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Watchlist?> GetTrackedByIdForUserAsync(Guid userId, Guid watchlistId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Watchlist>> GetUserWatchlistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsByNormalizedNameAsync(
            Guid userId,
            string normalizedName,
            Guid? excludeWatchlistId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Watchlist> AddAsync(Watchlist watchlist, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task TouchAsync(Guid watchlistId, DateTime utcNow, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SearchHistoryRepositoryStub : ISearchHistoryRepository
    {
        public Task RecordSearchAsync(
            Guid userId,
            string query,
            string normalizedQuery,
            DateTime utcNow,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(IReadOnlyList<SearchHistory> Items, int TotalCount)> GetUserHistoryAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid userId, Guid historyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class BlockingSetCache : ICacheService
    {
        private readonly Dictionary<string, object> _entries = [];

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? (T?)value : null);

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
            where T : class
        {
            _entries[key] = value;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class MemoryCache : ICacheService
    {
        private readonly Dictionary<string, object> _entries = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            where T : class =>
            Task.FromResult(_entries.TryGetValue(key, out var value) ? (T?)value : null);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
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
