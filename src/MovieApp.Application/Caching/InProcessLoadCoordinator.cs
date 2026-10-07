using System.Collections.Concurrent;

namespace MovieApp.Application.Caching;

/// <summary>
/// Coalesces in-process cache misses for the same key. Does not replace distributed cache or cross-instance locks.
/// </summary>
public sealed class InProcessLoadCoordinator<T>
{
    private readonly ConcurrentDictionary<string, Task<T>> _inFlight = new(StringComparer.Ordinal);

    public Task<T>? TryGetInFlight(string cacheKey) =>
        _inFlight.TryGetValue(cacheKey, out var task) ? task : null;

    public Task<T> RunInFlightAsync(string cacheKey, Func<Task<T>> load) =>
        _inFlight.GetOrAdd(cacheKey, _ => ExecuteAndRemoveAsync(cacheKey, load));

    private async Task<T> ExecuteAndRemoveAsync(string cacheKey, Func<Task<T>> load)
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
