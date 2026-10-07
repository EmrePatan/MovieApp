namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces in-process Insights V3 cache misses that share a cache key.
/// The key includes the per-user generation, so a watch write that bumps generation
/// does not join a build that started against the previous generation.
/// </summary>
public sealed class InsightsV3LoadCoordinator
{
    private readonly InProcessLoadCoordinator<Models.Insights.InsightsV3Result> _loads = new();

    public Task<Models.Insights.InsightsV3Result> RunAsync(
        string cacheKey,
        Func<Task<Models.Insights.InsightsV3Result>> load) =>
        _loads.RunInFlightAsync(cacheKey, load);
}
