namespace MovieApp.Infrastructure.Performance;

internal sealed class HomeColdPerfScope : IDisposable
{
    private static readonly AsyncLocal<HomeColdPerfMetrics?> Active = new();

    private readonly HomeColdPerfMetrics _metrics;

    private HomeColdPerfScope(string operation)
    {
        _metrics = new HomeColdPerfMetrics(operation);
        Active.Value = _metrics;
    }

    internal static HomeColdPerfMetrics? Current => Active.Value;

    public void Dispose()
    {
        if (Active.Value == _metrics)
        {
            Active.Value = null;
        }
    }

    internal HomeColdPerfMetrics Metrics => _metrics;

    internal string? CorrelationId { get; private set; }

    internal static HomeColdPerfScope Begin(string operation, string? correlationId)
    {
        var scope = new HomeColdPerfScope(operation);
        scope.CorrelationId = correlationId;
        return scope;
    }
}

internal sealed class HomeColdPerfMetrics(string operation)
{
    private int _activeCommands;
    private int _peakActiveCommands;

    public string Operation { get; } = operation;

    public int CommandCount { get; private set; }

    public long CommandExecutionMs { get; private set; }

    public long ConnectionOpenMs { get; private set; }

    public int PeakActiveCommands => _peakActiveCommands;

    public void RecordCommandCompleted(long elapsedMs)
    {
        CommandCount++;
        CommandExecutionMs += elapsedMs;
    }

    public void EnterCommand()
    {
        var active = Interlocked.Increment(ref _activeCommands);
        var peak = Volatile.Read(ref _peakActiveCommands);
        if (active > peak)
        {
            Interlocked.Exchange(ref _peakActiveCommands, active);
        }
    }

    public void ExitCommand()
    {
        Interlocked.Decrement(ref _activeCommands);
    }

    public void RecordConnectionOpen(long elapsedMs)
    {
        ConnectionOpenMs += elapsedMs;
    }

    public int RedisGetCount { get; private set; }

    public long RedisGetTransportMs { get; private set; }

    public long RedisDeserializeMs { get; private set; }

    public int RedisSetCount { get; private set; }

    public long RedisSetTransportMs { get; private set; }

    public long RedisSerializeMs { get; private set; }

    public long RedisMaxSingleGetTransportMs { get; private set; }

    public void RecordRedisGet(long transportMs, long deserializeMs)
    {
        RedisGetCount++;
        RedisGetTransportMs += transportMs;
        RedisDeserializeMs += deserializeMs;
        if (transportMs > RedisMaxSingleGetTransportMs)
        {
            RedisMaxSingleGetTransportMs = transportMs;
        }
    }

    public void RecordRedisSet(long serializeMs, long transportMs)
    {
        RedisSetCount++;
        RedisSerializeMs += serializeMs;
        RedisSetTransportMs += transportMs;
    }
}
