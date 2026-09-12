namespace MovieApp.Application.Abstractions.Caching;

public interface ISearchRefreshLockDiagnostics
{
    long RedisAcquireSuccess { get; }

    long RedisAcquireContention { get; }

    long LocalAcquireSuccess { get; }

    long LocalAcquireContention { get; }

    long RedisUnavailableFallbackToLocal { get; }
}
