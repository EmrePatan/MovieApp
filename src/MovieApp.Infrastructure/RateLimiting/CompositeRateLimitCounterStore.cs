using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.RateLimiting;

namespace MovieApp.Infrastructure.RateLimiting;

public sealed class CompositeRateLimitCounterStore(
    RedisRateLimitCounterStore redisRateLimitCounterStore,
    InMemoryRateLimitCounterStore inMemoryRateLimitCounterStore,
    ILogger<CompositeRateLimitCounterStore> logger) : IRateLimitCounterStore
{
    public async Task<RateLimitCounterResult> TryAcquireAsync(
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await redisRateLimitCounterStore.TryAcquireAsync(
                partitionKey,
                permitLimit,
                window,
                cancellationToken);
        }
        catch (Exception exception)
        {
            RedisRateLimitCounterLogMessages.LogFallbackToInMemory(logger, partitionKey, exception);

            return await inMemoryRateLimitCounterStore.TryAcquireAsync(
                partitionKey,
                permitLimit,
                window,
                cancellationToken);
        }
    }
}
