using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Abstractions.RateLimiting;

namespace MovieApp.Infrastructure.RateLimiting;

public sealed class CompositeRateLimitCounterStore(
    RedisRateLimitCounterStore redisRateLimitCounterStore,
    InMemoryRateLimitCounterStore inMemoryRateLimitCounterStore,
    IHostEnvironment environment,
    ILogger<CompositeRateLimitCounterStore> logger) : IRateLimitCounterStore
{
    private static readonly TimeSpan ProductionUnavailableRetryAfter = TimeSpan.FromSeconds(30);

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
            if (environment.IsProduction())
            {
                RedisRateLimitCounterLogMessages.LogProductionUnavailable(logger, partitionKey, exception);
                return new RateLimitCounterResult(false, ProductionUnavailableRetryAfter);
            }

            RedisRateLimitCounterLogMessages.LogFallbackToInMemory(logger, partitionKey, exception);

            return await inMemoryRateLimitCounterStore.TryAcquireAsync(
                partitionKey,
                permitLimit,
                window,
                cancellationToken);
        }
    }
}
