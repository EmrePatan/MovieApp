using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace MovieApp.Infrastructure.Identity;

/// <summary>
/// Short-lived, instance-local memory cache for JWT security-stamp checks. A detail page
/// fires several authenticated calls at once; without this, each one reads the users table.
/// Each API instance maintains its own entries (no distributed invalidation).
/// </summary>
public sealed class SecurityStampCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(5);

    private static readonly ConcurrentDictionary<Guid, PerUserStampLoadState> PerUser = new();

    public static string Key(Guid userId) => $"auth:security-stamp:{userId:N}";

    public static void Invalidate(IMemoryCache cache, Guid userId)
    {
        cache.Remove(Key(userId));
        var state = PerUser.GetOrAdd(userId, static _ => new PerUserStampLoadState());
        Interlocked.Increment(ref state.Generation);
    }

#pragma warning disable CA1822 // Registered as singleton; coalescing state is process-wide.
    public async Task<Guid?> GetOrLoadAsync(
        IMemoryCache cache,
        Guid userId,
        Func<CancellationToken, Task<Guid?>> load,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(Key(userId), out SecurityStampCacheEntry? cached) && cached is not null)
        {
            return cached.Stamp;
        }

        var state = PerUser.GetOrAdd(userId, static _ => new PerUserStampLoadState());
        var generation = Volatile.Read(ref state.Generation);
        var lazy = state.InflightByGeneration.GetOrAdd(
            generation,
            static (_, loadStamp) => new Lazy<Task<Guid?>>(() => loadStamp(CancellationToken.None)),
            load);

        try
        {
            var stamp = await lazy.Value.WaitAsync(cancellationToken);
            if (Volatile.Read(ref state.Generation) == generation)
            {
                cache.Set(
                    Key(userId),
                    new SecurityStampCacheEntry(stamp),
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });
            }

            return stamp;
        }
        finally
        {
            state.InflightByGeneration.TryRemove(new KeyValuePair<long, Lazy<Task<Guid?>>>(generation, lazy));
        }
    }
#pragma warning restore CA1822

    public sealed record SecurityStampCacheEntry(Guid? Stamp);

    private sealed class PerUserStampLoadState
    {
        internal long Generation;
        internal readonly ConcurrentDictionary<long, Lazy<Task<Guid?>>> InflightByGeneration = new();
    }
}
