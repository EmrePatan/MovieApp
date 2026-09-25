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

    private readonly ConcurrentDictionary<Guid, Lazy<Task<Guid?>>> _inflight = new();

    public static string Key(Guid userId) => $"auth:security-stamp:{userId:N}";

    public static void Invalidate(IMemoryCache cache, Guid userId) =>
        cache.Remove(Key(userId));

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

        var lazy = _inflight.GetOrAdd(
            userId,
            static (_, loadStamp) => new Lazy<Task<Guid?>>(() => loadStamp(CancellationToken.None)),
            load);

        try
        {
            var stamp = await lazy.Value.WaitAsync(cancellationToken);
            cache.Set(
                Key(userId),
                new SecurityStampCacheEntry(stamp),
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });
            return stamp;
        }
        finally
        {
            _inflight.TryRemove(userId, out _);
        }
    }

    public sealed record SecurityStampCacheEntry(Guid? Stamp);
}
