using Microsoft.Extensions.Logging;
using MovieApp.Api.BackgroundJobs;

namespace MovieApp.UnitTests.Observability;

public sealed class BackgroundJobOperationalRunnerTests
{
    [Fact]
    public async Task RunAsync_LogsStartAndCompletesSuccessfully()
    {
        var logger = new TestLogger();
        var executed = false;

        await BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.HotRelease,
            async () =>
            {
                executed = true;
                await Task.CompletedTask;
            });

        Assert.True(executed);
        Assert.Contains(logger.Entries, entry => entry.EventId == 6000);
    }

    [Fact]
    public async Task RunAsync_LogsFailureAndRethrows()
    {
        var logger = new TestLogger();
        var exception = new InvalidOperationException("job failed");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BackgroundJobOperationalRunner.RunAsync(
                logger,
                RecurringJobIds.HotRelease,
                () => throw exception));

        Assert.Same(exception, thrown);
        Assert.Contains(logger.Entries, entry => entry.EventId == 6000);
        Assert.Contains(logger.Entries, entry => entry.EventId == 6097);
    }

    private sealed class TestLogger : ILogger
    {
        public List<(int EventId, LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((eventId.Id, logLevel, formatter(state, exception)));
        }
    }
}
