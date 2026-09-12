using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.RateLimiting;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Configuration;
using StackExchange.Redis;

namespace MovieApp.Infrastructure.RateLimiting;

public sealed class RedisRateLimitCounterStore : IRateLimitCounterStore
{
    private const string AcquireScript = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
          redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        if current > tonumber(ARGV[2]) then
          local ttl = redis.call('TTL', KEYS[1])
          return { 0, ttl }
        end
        return { 1, 0 }
        """;

    private readonly IConnectionMultiplexer? _connectionMultiplexer;
    private readonly IOptions<RedisOptions> _redisOptions;
    private readonly ILogger<RedisRateLimitCounterStore> _logger;
    private readonly bool _useRedisBackend;

    public RedisRateLimitCounterStore(
        IServiceProvider serviceProvider,
        IOptions<RedisOptions> redisOptions,
        ILogger<RedisRateLimitCounterStore> logger)
    {
        _connectionMultiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
        _redisOptions = redisOptions;
        _logger = logger;
        _useRedisBackend = redisOptions.Value.IsConfigured() && _connectionMultiplexer is not null;
    }

    public async Task<RateLimitCounterResult> TryAcquireAsync(
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        if (!_useRedisBackend)
        {
            throw new InvalidOperationException("Redis rate limit store is not configured.");
        }

        var redisKey = BuildRedisKey(partitionKey);
        var windowSeconds = Math.Max(1, (int)Math.Ceiling(window.TotalSeconds));

        try
        {
            var database = _connectionMultiplexer!.GetDatabase();
            var result = (RedisResult[]?)await database.ScriptEvaluateAsync(
                AcquireScript,
                [redisKey],
                [windowSeconds, permitLimit]);

            if (result is null || result.Length < 2)
            {
                return new RateLimitCounterResult(false, window);
            }

            var acquired = (int)result[0] == 1;
            if (acquired)
            {
                return new RateLimitCounterResult(true, null);
            }

            var ttlSeconds = (int)result[1];
            var retryAfter = ttlSeconds > 0
                ? TimeSpan.FromSeconds(ttlSeconds)
                : window;

            return new RateLimitCounterResult(false, retryAfter);
        }
        catch (Exception exception) when (RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            RedisRateLimitCounterLogMessages.LogAcquireFailed(_logger, partitionKey, exception);
            throw;
        }
    }

    private string BuildRedisKey(string partitionKey) =>
        $"{_redisOptions.Value.InstanceName}ratelimit:{partitionKey}";
}
