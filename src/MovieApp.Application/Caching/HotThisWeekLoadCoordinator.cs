using System.Collections.Concurrent;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Caching;

public sealed class HotThisWeekLoadCoordinator
{
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<SearchItem>>> _inFlight =
        new(StringComparer.Ordinal);

    public Task<IReadOnlyList<SearchItem>>? TryGetInFlight(string cacheKey) =>
        _inFlight.TryGetValue(cacheKey, out var task) ? task : null;

    public Task<IReadOnlyList<SearchItem>> RunInFlightAsync(
        string cacheKey,
        Func<Task<IReadOnlyList<SearchItem>>> load) =>
        _inFlight.GetOrAdd(cacheKey, _ => ExecuteAndRemoveAsync(cacheKey, load));

    private async Task<IReadOnlyList<SearchItem>> ExecuteAndRemoveAsync(
        string cacheKey,
        Func<Task<IReadOnlyList<SearchItem>>> load)
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
