using MovieApp.Infrastructure.Performance;

namespace MovieApp.UnitTests.Performance;

public sealed class HomeColdPerfMetricsTests
{
    [Fact]
    public void PeakActiveCommands_TracksNestedCommandDepth()
    {
        using var scope = HomeColdPerfScope.Begin("Test", correlationId: null);
        var metrics = scope.Metrics;

        metrics.EnterCommand();
        metrics.EnterCommand();
        Assert.Equal(2, metrics.PeakActiveCommands);

        metrics.ExitCommand();
        metrics.RecordCommandCompleted(10);
        metrics.ExitCommand();
        metrics.RecordCommandCompleted(5);

        Assert.Equal(2, metrics.PeakActiveCommands);
        Assert.Equal(2, metrics.CommandCount);
        Assert.Equal(15, metrics.CommandExecutionMs);
    }

    [Fact]
    public void RedisMetrics_AccumulateGetAndSetTimings()
    {
        using var scope = HomeColdPerfScope.Begin("Test", correlationId: "corr");
        var metrics = scope.Metrics;

        metrics.RecordRedisGet(900, 12);
        metrics.RecordRedisGet(2900, 5);
        metrics.RecordRedisSet(3, 40);

        Assert.Equal(2, metrics.RedisGetCount);
        Assert.Equal(3800, metrics.RedisGetTransportMs);
        Assert.Equal(17, metrics.RedisDeserializeMs);
        Assert.Equal(2900, metrics.RedisMaxSingleGetTransportMs);
        Assert.Equal(1, metrics.RedisSetCount);
    }
}
