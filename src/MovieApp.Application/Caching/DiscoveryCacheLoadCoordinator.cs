using System.Collections.Concurrent;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces in-process discovery cache loads for the same cache key while a Redis lock owner is loading.
/// Does not replace distributed cache or Redis lock semantics across instances.
/// </summary>
public sealed class DiscoveryCacheLoadCoordinator
{
    private readonly ConcurrentDictionary<string, Task<PaginatedResult<SearchItem>>> _inFlight =
        new(StringComparer.Ordinal);

    public Task<PaginatedResult<SearchItem>>? TryGetInFlight(string cacheKey) =>
        _inFlight.TryGetValue(cacheKey, out var task) ? task : null;

    public Task<PaginatedResult<SearchItem>> RunInFlightAsync(
        string cacheKey,
        Func<Task<PaginatedResult<SearchItem>>> load) =>
        _inFlight.GetOrAdd(cacheKey, _ => ExecuteAndRemoveAsync(cacheKey, load));

    private async Task<PaginatedResult<SearchItem>> ExecuteAndRemoveAsync(
        string cacheKey,
        Func<Task<PaginatedResult<SearchItem>>> load)
    {
        try
        {
            return await load().ConfigureAwait(false);
        }
        finally
        {
            _inFlight.TryRemove(cacheKey, out _);
        }
    }
}
