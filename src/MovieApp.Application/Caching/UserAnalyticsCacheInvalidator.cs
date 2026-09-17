using MovieApp.Application.Abstractions.Caching;

namespace MovieApp.Application.Caching;

public sealed class UserAnalyticsCacheInvalidator(
    IProfileStatisticsCache profileStatisticsCache,
    IInsightsCache insightsCache) : IUserAnalyticsCacheInvalidator
{
    public async Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await profileStatisticsCache.InvalidateForUserAsync(userId, cancellationToken);
        await insightsCache.InvalidateForUserAsync(userId, cancellationToken);
    }
}
