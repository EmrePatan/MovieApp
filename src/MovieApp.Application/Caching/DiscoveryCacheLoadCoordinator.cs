using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces in-process discovery cache loads for the same cache key while a Redis lock owner is loading.
/// Does not replace distributed cache or Redis lock semantics across instances.
/// </summary>
public sealed class DiscoveryCacheLoadCoordinator
{
    private readonly InProcessLoadCoordinator<PaginatedResult<SearchItem>> _inner = new();

    public Task<PaginatedResult<SearchItem>>? TryGetInFlight(string cacheKey) =>
        _inner.TryGetInFlight(cacheKey);

    public Task<PaginatedResult<SearchItem>> RunInFlightAsync(
        string cacheKey,
        Func<Task<PaginatedResult<SearchItem>>> load) =>
        _inner.RunInFlightAsync(cacheKey, load);
}
