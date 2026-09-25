using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;

namespace MovieApp.Application.Caching;

public static class BackgroundAnalyticsInvalidation
{
    public static Task RunAsync(
        IUserAnalyticsCacheInvalidator invalidator,
        IServiceScopeFactory? scopeFactory,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (scopeFactory is null)
        {
            return invalidator.InvalidateForUserAsync(userId, cancellationToken);
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var scopedInvalidator = scope.ServiceProvider.GetRequiredService<IUserAnalyticsCacheInvalidator>();
                    await scopedInvalidator.InvalidateForUserAsync(userId, CancellationToken.None);
                }
                catch
                {
                    // Profile, insights, and recommendation caches are not required to finish the toggle.
                }
            },
            CancellationToken.None);

        return Task.CompletedTask;
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
