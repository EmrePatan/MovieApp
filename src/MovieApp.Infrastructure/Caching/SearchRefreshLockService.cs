using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;
using StackExchange.Redis;

namespace MovieApp.Infrastructure.Caching;

public sealed class SearchRefreshLockService : ISearchRefreshLockService
{
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
          return redis.call('del', KEYS[1])
        end
        return 0
        """;

    private const string RenewScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
          return redis.call('expire', KEYS[1], ARGV[2])
        end
        return 0
        """;

    private readonly IConnectionMultiplexer? _connectionMultiplexer;
    private readonly IOptions<RedisOptions> _redisOptions;
    private readonly LocalSearchRefreshSingleFlightGate _localSingleFlightGate;
    private readonly SearchRefreshLockDiagnostics _diagnostics;
    private readonly ILogger<SearchRefreshLockService> _logger;
    private readonly bool _useRedisBackend;

    public SearchRefreshLockService(
        IServiceProvider serviceProvider,
        IOptions<RedisOptions> redisOptions,
        LocalSearchRefreshSingleFlightGate localSingleFlightGate,
        SearchRefreshLockDiagnostics diagnostics,
        ILogger<SearchRefreshLockService> logger)
    {
        _connectionMultiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
        _redisOptions = redisOptions;
        _localSingleFlightGate = localSingleFlightGate;
        _diagnostics = diagnostics;
        _logger = logger;
        _useRedisBackend = redisOptions.Value.IsConfigured() && _connectionMultiplexer is not null;
    }

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
                _diagnostics.RecordRedisAcquireSuccess();
                return new SearchRefreshLockHandle(
                    lockKey,
                    redisResult.Token!,
                    SearchRefreshLockBackend.Redis);
            }

            if (redisResult.Contended)
            {
                _diagnostics.RecordRedisAcquireContention();
                return null;
            }

            _diagnostics.RecordRedisUnavailableFallbackToLocal();
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
            _localSingleFlightGate.Release(lockKey, lockToken);
            return;
        }

        if (!_useRedisBackend || string.IsNullOrWhiteSpace(lockToken))
        {
            return;
        }

        var redisKey = BuildRedisKey(lockKey);

        try
        {
            var database = _connectionMultiplexer!.GetDatabase();
            await database.ScriptEvaluateAsync(
                ReleaseScript,
                [redisKey],
                [lockToken]);
        }
        catch (Exception exception) when (RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            RedisSearchRefreshLockLogMessages.LogLockReleaseFailed(_logger, lockKey, exception);
        }
    }

    public async Task<bool> TryRenewAsync(
        string lockKey,
        string lockToken,
        SearchRefreshLockBackend backend,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        if (backend == SearchRefreshLockBackend.LocalSingleFlight)
        {
            return _localSingleFlightGate.VerifyOwnership(lockKey, lockToken);
        }

        if (!_useRedisBackend || string.IsNullOrWhiteSpace(lockToken))
        {
            return false;
        }

        var redisKey = BuildRedisKey(lockKey);
        var lockSeconds = Math.Max(1, (int)Math.Ceiling(lockDuration.TotalSeconds));

        try
        {
            var database = _connectionMultiplexer!.GetDatabase();
            var renewed = (int)await database.ScriptEvaluateAsync(
                RenewScript,
                [redisKey],
                [lockToken, lockSeconds]);

            return renewed == 1;
        }
        catch (Exception exception) when (RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            RedisSearchRefreshLockLogMessages.LogLockRenewFailed(_logger, lockKey, exception);
            return false;
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
            var database = _connectionMultiplexer!.GetDatabase();
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
            RedisSearchRefreshLockLogMessages.LogLockAcquisitionFailed(_logger, lockKey, exception);
            return (false, false, null);
        }
    }

    private SearchRefreshLockHandle? TryAcquireLocal(string lockKey)
    {
        if (_localSingleFlightGate.TryAcquire(lockKey, out var lockToken))
        {
            _diagnostics.RecordLocalAcquireSuccess();
            return new SearchRefreshLockHandle(
                lockKey,
                lockToken,
                SearchRefreshLockBackend.LocalSingleFlight);
        }

        _diagnostics.RecordLocalAcquireContention();
        return null;
    }

    private string BuildRedisKey(string lockKey) => $"{_redisOptions.Value.InstanceName}{lockKey}";
}
