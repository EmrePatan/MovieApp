using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Exceptions;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;
using StackExchange.Redis;

namespace MovieApp.Infrastructure.Caching;

public sealed class RedisSearchRefreshLockService(
    IConnectionMultiplexer? connectionMultiplexer,
    IOptions<RedisOptions> redisOptions,
    ILogger<RedisSearchRefreshLockService> logger) : ISearchRefreshLockService
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
        if (!_useRedisBackend)
        {
            return CreateFailOpenHandle(lockKey);
        }

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

            if (!acquired)
            {
                return null;
            }

            return new SearchRefreshLockHandle(lockKey, token);
        }
        catch (Exception exception) when (RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            RedisSearchRefreshLockLogMessages.LogLockAcquisitionFailed(logger, lockKey, exception);
            return CreateFailOpenHandle(lockKey);
        }
    }

    public async Task ReleaseAsync(
        string lockKey,
        string lockToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lockToken))
        {
            return;
        }

        if (!_useRedisBackend)
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

    private static SearchRefreshLockHandle CreateFailOpenHandle(string lockKey) =>
        new(lockKey, Guid.NewGuid().ToString("N"));

    private string BuildRedisKey(string lockKey) => $"{redisOptions.Value.InstanceName}{lockKey}";
}
