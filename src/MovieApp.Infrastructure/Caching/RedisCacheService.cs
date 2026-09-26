using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Infrastructure.Configuration;
using MovieApp.Infrastructure.Performance;

namespace MovieApp.Infrastructure.Caching;

public sealed class RedisCacheService(
    IDistributedCache distributedCache,
    IOptions<RedisOptions> redisOptions,
    ILogger<RedisCacheService> logger,
    RedisCacheFailureLogger failureLogger) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly bool _useRedisBackend = redisOptions.Value.IsConfigured();

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        var cacheKey = BuildCacheKey(key);

        try
        {
            var transportStopwatch = Stopwatch.StartNew();
            var cachedValue = await distributedCache.GetStringAsync(cacheKey, cancellationToken);
            transportStopwatch.Stop();

            if (string.IsNullOrWhiteSpace(cachedValue))
            {
                HomeColdPerfScope.Current?.RecordRedisGet(transportStopwatch.ElapsedMilliseconds, 0);
                return null;
            }

            var deserializeStopwatch = Stopwatch.StartNew();
            var result = JsonSerializer.Deserialize<T>(cachedValue, SerializerOptions);
            deserializeStopwatch.Stop();
            HomeColdPerfScope.Current?.RecordRedisGet(
                transportStopwatch.ElapsedMilliseconds,
                deserializeStopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception exception) when (_useRedisBackend && RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            failureLogger.LogGetFailure(logger, exception);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var cacheKey = BuildCacheKey(key);
        var serializeStopwatch = Stopwatch.StartNew();
        var serializedValue = JsonSerializer.Serialize(value, SerializerOptions);
        serializeStopwatch.Stop();

        var options = new DistributedCacheEntryOptions();

        if (expiry.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiry;
        }

        try
        {
            var transportStopwatch = Stopwatch.StartNew();
            await distributedCache.SetStringAsync(cacheKey, serializedValue, options, cancellationToken);
            transportStopwatch.Stop();
            HomeColdPerfScope.Current?.RecordRedisSet(
                serializeStopwatch.ElapsedMilliseconds,
                transportStopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (_useRedisBackend && RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            failureLogger.LogSetFailure(logger, exception);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(key);

        try
        {
            await distributedCache.RemoveAsync(cacheKey, cancellationToken);
        }
        catch (Exception exception) when (_useRedisBackend && RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            failureLogger.LogRemoveFailure(logger, exception);
        }
    }

    private string BuildCacheKey(string key) => $"{redisOptions.Value.InstanceName}{key}";
}
