using MovieApp.Application.Abstractions.Caching;
using System.Threading;

namespace MovieApp.Infrastructure.Caching;

public sealed class SearchRefreshLockDiagnostics : ISearchRefreshLockDiagnostics
{
    private long _redisAcquireSuccess;
    private long _redisAcquireContention;
    private long _localAcquireSuccess;
    private long _localAcquireContention;
    private long _redisUnavailableFallbackToLocal;

    public long RedisAcquireSuccess => Interlocked.Read(ref _redisAcquireSuccess);

    public long RedisAcquireContention => Interlocked.Read(ref _redisAcquireContention);

    public long LocalAcquireSuccess => Interlocked.Read(ref _localAcquireSuccess);

    public long LocalAcquireContention => Interlocked.Read(ref _localAcquireContention);

    public long RedisUnavailableFallbackToLocal => Interlocked.Read(ref _redisUnavailableFallbackToLocal);

    public void RecordRedisAcquireSuccess() => Interlocked.Increment(ref _redisAcquireSuccess);

    public void RecordRedisAcquireContention() => Interlocked.Increment(ref _redisAcquireContention);

    public void RecordLocalAcquireSuccess() => Interlocked.Increment(ref _localAcquireSuccess);

    public void RecordLocalAcquireContention() => Interlocked.Increment(ref _localAcquireContention);

    public void RecordRedisUnavailableFallbackToLocal() =>
        Interlocked.Increment(ref _redisUnavailableFallbackToLocal);
}
