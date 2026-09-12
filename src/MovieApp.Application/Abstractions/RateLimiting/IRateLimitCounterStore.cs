namespace MovieApp.Application.Abstractions.RateLimiting;

public interface IRateLimitCounterStore
{
    Task<RateLimitCounterResult> TryAcquireAsync(
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}

public sealed record RateLimitCounterResult(bool IsAcquired, TimeSpan? RetryAfter);
