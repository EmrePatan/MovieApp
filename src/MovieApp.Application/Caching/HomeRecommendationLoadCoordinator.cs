namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces in-process personalized home recommendation misses that share a cache key.
/// The key includes the per-user generation, so a library write that bumps generation
/// does not join a build that started against the previous generation.
/// </summary>
public sealed class HomeRecommendationLoadCoordinator
{
    private readonly InProcessLoadCoordinator<IReadOnlyList<Models.Recommendations.RecommendationSection>> _loads = new();

    public Task<IReadOnlyList<Models.Recommendations.RecommendationSection>> RunAsync(
        string cacheKey,
        Func<Task<IReadOnlyList<Models.Recommendations.RecommendationSection>>> load) =>
        _loads.RunInFlightAsync(cacheKey, load);
}
