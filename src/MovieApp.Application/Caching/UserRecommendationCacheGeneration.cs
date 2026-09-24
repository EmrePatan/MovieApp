using MovieApp.Application.Abstractions.Caching;

namespace MovieApp.Application.Caching;

/// <summary>
/// Per-user generation stamped into personalized recommendation cache keys. Bumping it on
/// watch/watchlist/favorite writes makes every cached personalized list for that user
/// (all locales and pages) unreachable, so exclusions apply on the next request.
/// </summary>
public sealed class UserRecommendationCacheGeneration(ICacheService cacheService)
{
    private static readonly TimeSpan GenerationTtl = TimeSpan.FromDays(30);

    public async Task<long> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var current = await cacheService.GetAsync<RecommendationGenerationState>(
            RecommendationCacheKeys.Generation(userId),
            cancellationToken);
        return current?.Value ?? 0;
    }

    public async Task InvalidateForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var next = await GetAsync(userId, cancellationToken) + 1;
            await cacheService.SetAsync(
                RecommendationCacheKeys.Generation(userId),
                new RecommendationGenerationState(next),
                GenerationTtl,
                cancellationToken);
        }
        catch
        {
            // Cache invalidation must not fail the originating write operation.
        }
    }
}

public sealed record RecommendationGenerationState(long Value);
