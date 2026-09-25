using System.Collections.Concurrent;
using MovieApp.Application.Abstractions.RateLimiting;

namespace MovieApp.Infrastructure.RateLimiting;

public sealed class InMemoryRateLimitCounterStore : IRateLimitCounterStore
{
    internal const int MaxTrackedPartitions = 4_096;

    internal const int HardTrackedPartitionCap = 50_000;

    private readonly ConcurrentDictionary<string, WindowCounter> _counters = new(StringComparer.Ordinal);

    private long _nextEvictionTicks;

    internal int TrackedPartitionCount => _counters.Count;

    public Task<RateLimitCounterResult> TryAcquireAsync(
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        EvictExpiredIfNeeded(now);

        if (!_counters.ContainsKey(partitionKey) && _counters.Count >= HardTrackedPartitionCap)
        {
            EvictExpired(now);
            if (!_counters.ContainsKey(partitionKey) && _counters.Count >= HardTrackedPartitionCap)
            {
                return Task.FromResult(new RateLimitCounterResult(false, window));
            }
        }

        var counter = _counters.GetOrAdd(partitionKey, static _ => new WindowCounter());

        lock (counter.Sync)
        {
            if (counter.WindowStartUtc is null || now - counter.WindowStartUtc.Value >= window)
            {
                counter.WindowStartUtc = now;
                counter.WindowEndUtc = AddWindow(now, window);
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

    private void EvictExpiredIfNeeded(DateTime utcNow)
    {
        if (_counters.Count <= MaxTrackedPartitions)
        {
            return;
        }

        var nowTicks = utcNow.Ticks;
        if (nowTicks < Volatile.Read(ref _nextEvictionTicks))
        {
            return;
        }

        Volatile.Write(ref _nextEvictionTicks, (utcNow + TimeSpan.FromSeconds(1)).Ticks);
        EvictExpired(utcNow);
    }

    private static DateTime AddWindow(DateTime utcNow, TimeSpan window)
    {
        if (window <= TimeSpan.Zero)
        {
            return utcNow;
        }

        var maxTicks = DateTime.MaxValue.Ticks - utcNow.Ticks;
        return window.Ticks >= maxTicks ? DateTime.MaxValue : utcNow + window;
    }

    private void EvictExpired(DateTime utcNow)
    {
        foreach (var pair in _counters)
        {
            var counter = pair.Value;
            lock (counter.Sync)
            {
                if (counter.WindowEndUtc <= utcNow)
                {
                    _counters.TryRemove(new KeyValuePair<string, WindowCounter>(pair.Key, counter));
                }
            }
        }
    }

    private sealed class WindowCounter
    {
        public object Sync { get; } = new();

        public DateTime? WindowStartUtc { get; set; }

        public DateTime WindowEndUtc { get; set; } = DateTime.MaxValue;

        public int Count { get; set; }
    }
}
