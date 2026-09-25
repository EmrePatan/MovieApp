using MovieApp.Application.Abstractions.Caching;

namespace MovieApp.Application.Caching;

public sealed class UserAnalyticsCacheInvalidator(
    IProfileStatisticsCache profileStatisticsCache,
    IInsightsCache insightsCache,
    ICacheService cacheService) : IUserAnalyticsCacheInvalidator
{
    private readonly UserRecommendationCacheGeneration _recommendationCacheGeneration = new(cacheService);

    public async Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            profileStatisticsCache.InvalidateForUserAsync(userId, cancellationToken),
            insightsCache.InvalidateForUserAsync(userId, cancellationToken),
            _recommendationCacheGeneration.InvalidateForUserAsync(userId, cancellationToken));
    }
}
