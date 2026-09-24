using System.Collections.Concurrent;
using MovieApp.Application.Abstractions.ExternalRatings;

namespace MovieApp.Infrastructure.Caching;

/// <summary>
/// Per-title in-process coalescing for MDBList refresh attempts on a single instance.
/// </summary>
public sealed class ExternalRatingsRefreshCoalescer : IExternalRatingsRefreshCoalescer
{
    private readonly ConcurrentDictionary<string, Task> _inflight = new(StringComparer.Ordinal);

    public Task CoalesceAsync(string key, Func<Task> factory)
    {
        var task = _inflight.GetOrAdd(key, _ => ExecuteAndRemoveAsync(key, factory));
        return task;
    }

    private async Task ExecuteAndRemoveAsync(string key, Func<Task> factory)
    {
        try
        {
            await factory().ConfigureAwait(false);
        }
        finally
        {
            _inflight.TryRemove(key, out _);
        }
    }
}
