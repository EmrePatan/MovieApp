using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;

namespace MovieApp.Application.Caching;

public static class BackgroundAnalyticsInvalidation
{
    public static async Task RunAsync(
        IUserAnalyticsCacheInvalidator invalidator,
        IServiceScopeFactory? scopeFactory,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (scopeFactory is null)
        {
            await invalidator.InvalidateForUserAsync(userId, cancellationToken);
            return;
        }

        // Recommendation and insights generations are part of the 12h cache keys.
        // Bump both before the write returns. Profile statistics stay off this path.
        try
        {
            using var scope = scopeFactory.CreateScope();
            var cache = scope.ServiceProvider.GetService<ICacheService>();
            if (cache is not null)
            {
                await BumpBothGenerationsAsync(cache, userId, CancellationToken.None);
                scope.ServiceProvider.GetService<IPersonalizedCacheRebuildScheduler>()?.Schedule(userId);
            }
        }
        catch
        {
            // A missed bump falls back to the cache TTL.
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var profile = scope.ServiceProvider.GetService<IProfileStatisticsCache>();
                    if (profile is not null)
                    {
                        await profile.InvalidateForUserAsync(userId, CancellationToken.None);
                    }
                }
                catch
                {
                    // Profile statistics are not required to finish the toggle.
                }
            },
            CancellationToken.None);
    }

    public static async Task InvalidateRecommendationsAsync(
        ICacheService cacheService,
        IServiceScopeFactory? scopeFactory,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (scopeFactory is null)
        {
            await new UserRecommendationCacheGeneration(cacheService)
                .InvalidateForUserAsync(userId, cancellationToken);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var cache = scope.ServiceProvider.GetService<ICacheService>() ?? cacheService;
        await new UserRecommendationCacheGeneration(cache)
            .InvalidateForUserAsync(userId, CancellationToken.None);
        scope.ServiceProvider.GetService<IPersonalizedCacheRebuildScheduler>()?.Schedule(userId);
    }

    public static async Task BumpBothGenerationsAsync(
        ICacheService cacheService,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await new UserRecommendationCacheGeneration(cacheService)
            .InvalidateForUserAsync(userId, cancellationToken);
        await new InsightsCacheGeneration(cacheService)
            .InvalidateForUserAsync(userId, cancellationToken);
    }
}
