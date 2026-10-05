using System.Collections.Concurrent;
using MovieApp.Application.Models.Home;

namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces in-process Home cache loads for the same cache key while a Redis lock owner is loading.
/// Does not replace distributed cache or Redis lock semantics across instances.
/// </summary>
public sealed class HomeLoadCoordinator
{
    private readonly ConcurrentDictionary<string, Task<HomeResult>> _inFlight =
        new(StringComparer.Ordinal);

    public Task<HomeResult>? TryGetInFlight(string cacheKey)
    {
        if (!_inFlight.TryGetValue(cacheKey, out var task))
        {
            return null;
        }

        if (task.IsCompleted)
        {
            _inFlight.TryRemove(cacheKey, out _);
            return null;
        }

        return task;
    }

    public Task<HomeResult> RunInFlightAsync(string cacheKey, Func<Task<HomeResult>> load)
    {
        if (_inFlight.TryGetValue(cacheKey, out var existing) && !existing.IsCompleted)
        {
            return existing;
        }

        if (existing is not null)
        {
            _inFlight.TryRemove(cacheKey, out _);
        }

        return _inFlight.GetOrAdd(cacheKey, _ => ExecuteAndRemoveAsync(cacheKey, load));
    }

    private async Task<HomeResult> ExecuteAndRemoveAsync(string cacheKey, Func<Task<HomeResult>> load)
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
