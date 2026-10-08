namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces in-process keyword discover searches that share a query, locale, and page.
/// </summary>
public sealed class KeywordDiscoverSearchLoadCoordinator
{
    private readonly InProcessLoadCoordinator<Models.Movies.PaginatedResult<Models.Discovery.KeywordDiscoverItem>> _loads = new();

    public Task<Models.Movies.PaginatedResult<Models.Discovery.KeywordDiscoverItem>> RunAsync(
        string cacheKey,
        Func<Task<Models.Movies.PaginatedResult<Models.Discovery.KeywordDiscoverItem>>> load) =>
        _loads.RunInFlightAsync(cacheKey, load);
}
