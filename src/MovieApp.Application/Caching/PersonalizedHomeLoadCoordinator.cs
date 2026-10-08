namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces in-process <c>GET /api/home/personalized</c> misses for the same user, locale,
/// section size, region, and recommendation generation.
/// </summary>
public sealed class PersonalizedHomeLoadCoordinator
{
    private readonly InProcessLoadCoordinator<Models.Home.HomePersonalizedResult> _loads = new();

    public Task<Models.Home.HomePersonalizedResult> RunAsync(
        string cacheKey,
        Func<Task<Models.Home.HomePersonalizedResult>> load) =>
        _loads.RunInFlightAsync(cacheKey, load);
}
