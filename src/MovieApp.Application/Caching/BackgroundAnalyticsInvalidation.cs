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

        // The library-search cache key includes this generation. Bump it before the write
        // returns so the next search misses. Profile and insights invalidation stay off
        // the favorite/watch critical path.
        try
        {
            using var scope = scopeFactory.CreateScope();
            var cache = scope.ServiceProvider.GetService<ICacheService>();
            if (cache is not null)
            {
                await new UserRecommendationCacheGeneration(cache)
                    .InvalidateForUserAsync(userId, CancellationToken.None);
            }
        }
        catch
        {
            // A missed bump falls back to the short search TTL.
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var profile = scope.ServiceProvider.GetService<IProfileStatisticsCache>();
                    var insights = scope.ServiceProvider.GetService<IInsightsCache>();
                    if (profile is not null)
                    {
                        await profile.InvalidateForUserAsync(userId, CancellationToken.None);
                    }

                    if (insights is not null)
                    {
                        await insights.InvalidateForUserAsync(userId, CancellationToken.None);
                    }
                }
                catch
                {
                    // Profile and insights caches are not required to finish the toggle.
                }
            },
            CancellationToken.None);
    }

    public static Task InvalidateRecommendationsAsync(
        ICacheService cacheService,
        IServiceScopeFactory? scopeFactory,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (scopeFactory is null)
        {
            return new UserRecommendationCacheGeneration(cacheService)
                .InvalidateForUserAsync(userId, cancellationToken);
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var scopedCache = scope.ServiceProvider.GetRequiredService<ICacheService>();
                    await new UserRecommendationCacheGeneration(scopedCache)
                        .InvalidateForUserAsync(userId, CancellationToken.None);
                }
                catch
                {
                    // Recommendation freshness is not required to finish the follow toggle.
                }
            },
            CancellationToken.None);

        return Task.CompletedTask;
    }
}
