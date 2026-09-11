using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Infrastructure.Configuration;

namespace MovieApp.Infrastructure.Caching;

public sealed class RedisCacheService(
    IDistributedCache distributedCache,
    IOptions<RedisOptions> redisOptions) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        var cacheKey = BuildCacheKey(key);
        var cachedValue = await distributedCache.GetStringAsync(cacheKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(cachedValue))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(cachedValue, SerializerOptions);
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

        await distributedCache.SetStringAsync(cacheKey, serializedValue, options, cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(key);
        await distributedCache.RemoveAsync(cacheKey, cancellationToken);
    }

    private string BuildCacheKey(string key) => $"{redisOptions.Value.InstanceName}{key}";
}
