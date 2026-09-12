using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Infrastructure.Configuration;

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
            var cachedValue = await distributedCache.GetStringAsync(cacheKey, cancellationToken);

            if (string.IsNullOrWhiteSpace(cachedValue))
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(cachedValue, SerializerOptions);
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
        var serializedValue = JsonSerializer.Serialize(value, SerializerOptions);

        var options = new DistributedCacheEntryOptions();

        if (expiry.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = expiry;
        }

        try
        {
            await distributedCache.SetStringAsync(cacheKey, serializedValue, options, cancellationToken);
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
