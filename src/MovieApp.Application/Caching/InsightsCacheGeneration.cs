using MovieApp.Application.Abstractions.Caching;

namespace MovieApp.Application.Caching;

/// <summary>
/// Per-user generation stamped into insights v3 cache keys.
/// </summary>
public sealed class InsightsCacheGeneration(ICacheService cacheService)
{
    private static readonly TimeSpan GenerationTtl = TimeSpan.FromDays(30);

    public async Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var generationKey = InsightsCacheKeys.Generation(userId);
            var current = await cacheService.GetAsync<InsightsGenerationState>(
                generationKey,
                cancellationToken);
            var nextGeneration = (current?.Value ?? 0) + 1;
            await cacheService.SetAsync(
                generationKey,
                new InsightsGenerationState(nextGeneration),
                GenerationTtl,
                cancellationToken);
        }
        catch
        {
            // Cache invalidation must not fail the originating write operation.
        }
    }
}
