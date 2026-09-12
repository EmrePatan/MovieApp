using System.Collections.Concurrent;
using MovieApp.Application.Abstractions.RateLimiting;

namespace MovieApp.Infrastructure.RateLimiting;

public sealed class InMemoryRateLimitCounterStore : IRateLimitCounterStore
{
    private readonly ConcurrentDictionary<string, WindowCounter> _counters = new(StringComparer.Ordinal);

    public Task<RateLimitCounterResult> TryAcquireAsync(
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var counter = _counters.GetOrAdd(partitionKey, static _ => new WindowCounter());

        lock (counter.Sync)
        {
            if (counter.WindowStartUtc is null || now - counter.WindowStartUtc.Value >= window)
            {
                counter.WindowStartUtc = now;
                counter.Count = 0;
            }

            counter.Count++;

            if (counter.Count > permitLimit)
            {
                var retryAfter = window - (now - counter.WindowStartUtc.Value);
                if (retryAfter < TimeSpan.Zero)
                {
                    retryAfter = TimeSpan.Zero;
                }

                return Task.FromResult(new RateLimitCounterResult(false, retryAfter));
            }

            return Task.FromResult(new RateLimitCounterResult(true, null));
        }
    }

    private sealed class WindowCounter
    {
        public object Sync { get; } = new();

        public DateTime? WindowStartUtc { get; set; }

        public int Count { get; set; }
    }
}
