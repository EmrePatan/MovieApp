using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces in-process explore-preview misses so concurrent Keşfet hub loads share one rail fan-out.
/// </summary>
public sealed class ExplorePreviewLoadCoordinator
{
    private readonly InProcessLoadCoordinator<ExplorePreviewResult> _inner = new();

    public Task<ExplorePreviewResult> RunInFlightAsync(
        string cacheKey,
        Func<Task<ExplorePreviewResult>> load) =>
        _inner.RunInFlightAsync(cacheKey, load);
}
