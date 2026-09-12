using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;
using StackExchange.Redis;

namespace MovieApp.Infrastructure.Caching;

public sealed class SearchRefreshLockService(
    IConnectionMultiplexer? connectionMultiplexer,
    IOptions<RedisOptions> redisOptions,
    LocalSearchRefreshSingleFlightGate localSingleFlightGate,
    SearchRefreshLockDiagnostics diagnostics,
    ILogger<SearchRefreshLockService> logger) : ISearchRefreshLockService
{
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
          return redis.call('del', KEYS[1])
        end
        return 0
        """;

    private readonly bool _useRedisBackend = redisOptions.Value.IsConfigured() && connectionMultiplexer is not null;

    public async Task<SearchRefreshLockHandle?> TryAcquireAsync(
        string lockKey,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        if (_useRedisBackend)
        {
            var redisResult = await TryAcquireRedisAsync(lockKey, lockDuration, cancellationToken);
            if (redisResult.Acquired)
            {
                diagnostics.RecordRedisAcquireSuccess();
                return new SearchRefreshLockHandle(
                    lockKey,
                    redisResult.Token!,
                    SearchRefreshLockBackend.Redis);
            }

            if (redisResult.Contended)
            {
                diagnostics.RecordRedisAcquireContention();
                return null;
            }

            diagnostics.RecordRedisUnavailableFallbackToLocal();
        }

        return TryAcquireLocal(lockKey);
    }

    public async Task ReleaseAsync(
        string lockKey,
        string lockToken,
        SearchRefreshLockBackend backend,
        CancellationToken cancellationToken = default)
    {
        if (backend == SearchRefreshLockBackend.LocalSingleFlight)
        {
            localSingleFlightGate.Release(lockKey, lockToken);
            return;
        }

        if (!_useRedisBackend || string.IsNullOrWhiteSpace(lockToken))
        {
            return;
        }

        var redisKey = BuildRedisKey(lockKey);

        try
        {
            var database = connectionMultiplexer!.GetDatabase();
            await database.ScriptEvaluateAsync(
                ReleaseScript,
                [redisKey],
                [lockToken]);
        }
        catch (Exception exception) when (RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            RedisSearchRefreshLockLogMessages.LogLockReleaseFailed(logger, lockKey, exception);
        }
    }

    private async Task<(bool Acquired, bool Contended, string? Token)> TryAcquireRedisAsync(
        string lockKey,
        TimeSpan lockDuration,
        CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        var redisKey = BuildRedisKey(lockKey);

        try
        {
            var database = connectionMultiplexer!.GetDatabase();
            var acquired = await database.StringSetAsync(
                redisKey,
                token,
                lockDuration,
                When.NotExists);

            if (acquired)
            {
                return (true, false, token);
            }

            return (false, true, null);
        }
        catch (Exception exception) when (RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            RedisSearchRefreshLockLogMessages.LogLockAcquisitionFailed(logger, lockKey, exception);
            return (false, false, null);
        }
    }

    private SearchRefreshLockHandle? TryAcquireLocal(string lockKey)
    {
        if (localSingleFlightGate.TryAcquire(lockKey, out var lockToken))
        {
            diagnostics.RecordLocalAcquireSuccess();
            return new SearchRefreshLockHandle(
                lockKey,
                lockToken,
                SearchRefreshLockBackend.LocalSingleFlight);
        }

        diagnostics.RecordLocalAcquireContention();
        return null;
    }

    private string BuildRedisKey(string lockKey) => $"{redisOptions.Value.InstanceName}{lockKey}";
}
