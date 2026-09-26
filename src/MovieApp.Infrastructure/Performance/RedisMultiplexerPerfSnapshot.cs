using StackExchange.Redis;

namespace MovieApp.Infrastructure.Performance;

internal static class RedisMultiplexerPerfSnapshot
{
    internal static RedisMuxPerfValues? TryCapture(IConnectionMultiplexer? multiplexer)
    {
        if (multiplexer is null)
        {
            return null;
        }

        try
        {
            var counters = multiplexer.GetCounters();
            return new RedisMuxPerfValues(
                multiplexer.IsConnected,
                multiplexer.IsConnecting,
                (int)Math.Min(int.MaxValue, counters.TotalOutstanding),
                counters.Interactive.SentItemsAwaitingResponse,
                counters.Interactive.CompletedAsynchronously,
                counters.Interactive.FailedAsynchronously);
        }
        catch
        {
            return new RedisMuxPerfValues(
                multiplexer.IsConnected,
                multiplexer.IsConnecting,
                -1,
                -1,
                -1,
                -1);
        }
    }
}

internal readonly record struct RedisMuxPerfValues(
    bool IsConnected,
    bool IsConnecting,
    int TotalOutstanding,
    int InteractiveAwaitingResponse,
    long InteractiveCompletedAsynchronously,
    long InteractiveFailedAsynchronously);
