using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Infrastructure.Configuration;
using StackExchange.Redis;

namespace MovieApp.Infrastructure.Caching;

public sealed class SearchRefreshCompletionSignal(
    IConnectionMultiplexer? connectionMultiplexer,
    IOptions<RedisOptions> redisOptions,
    LocalSearchRefreshCompletionRegistry localRegistry,
    ILogger<SearchRefreshCompletionSignal> logger) : ISearchRefreshCompletionSignal
{
    private const string SucceededValue = "s";
    private const string FailedValue = "f";

    private readonly bool _useRedisBackend = redisOptions.Value.IsConfigured() && connectionMultiplexer is not null;

    public async Task PublishAsync(
        string lockKey,
        SearchRefreshAttemptOutcome outcome,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var completionKey = SearchRefreshCompletionKeys.FromLockKey(lockKey);

        if (_useRedisBackend)
        {
            var published = await TryPublishRedisAsync(completionKey, outcome, ttl, cancellationToken);
            if (published)
            {
                return;
            }
        }

        localRegistry.Publish(completionKey, outcome, ttl);
    }

    public async Task<SearchRefreshAttemptOutcome?> TryGetOutcomeAsync(
        string lockKey,
        CancellationToken cancellationToken = default)
    {
        var completionKey = SearchRefreshCompletionKeys.FromLockKey(lockKey);

        if (_useRedisBackend)
        {
            var redisOutcome = await TryGetRedisOutcomeAsync(completionKey, cancellationToken);
            if (redisOutcome.HasValue)
            {
                return redisOutcome;
            }
        }

        return localRegistry.TryGet(completionKey);
    }

    private async Task<bool> TryPublishRedisAsync(
        string completionKey,
        SearchRefreshAttemptOutcome outcome,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        try
        {
            var database = connectionMultiplexer!.GetDatabase();
            await database.StringSetAsync(
                BuildRedisKey(completionKey),
                outcome == SearchRefreshAttemptOutcome.Succeeded ? SucceededValue : FailedValue,
                ttl);

            return true;
        }
        catch (Exception exception) when (RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            RedisSearchRefreshCompletionLogMessages.LogPublishFailed(logger, completionKey, exception);
            return false;
        }
    }

    private async Task<SearchRefreshAttemptOutcome?> TryGetRedisOutcomeAsync(
        string completionKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var database = connectionMultiplexer!.GetDatabase();
            var value = await database.StringGetAsync(BuildRedisKey(completionKey));

            if (value.IsNullOrEmpty)
            {
                return null;
            }

            return value.ToString() switch
            {
                SucceededValue => SearchRefreshAttemptOutcome.Succeeded,
                FailedValue => SearchRefreshAttemptOutcome.Failed,
                _ => null
            };
        }
        catch (Exception exception) when (RedisCacheExceptionClassifier.IsRedisInfrastructureFailure(exception))
        {
            RedisSearchRefreshCompletionLogMessages.LogReadFailed(logger, completionKey, exception);
            return null;
        }
    }

    private string BuildRedisKey(string completionKey) => $"{redisOptions.Value.InstanceName}{completionKey}";
}
