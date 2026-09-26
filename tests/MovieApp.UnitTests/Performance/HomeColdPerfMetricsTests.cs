using MovieApp.Infrastructure.Performance;

namespace MovieApp.UnitTests.Performance;

public sealed class HomeColdPerfMetricsTests
{
    [Fact]
    public void PeakActiveCommands_TracksNestedCommandDepth()
    {
        using var scope = HomeColdPerfScope.Begin("Test");
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
}
