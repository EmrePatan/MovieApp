using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsV3RepositoryPhaseSchedulerTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(11, 11)]
    public async Task RunAllAsyncNeverExceedsConfiguredConcurrency(int maxConcurrency, int expectedPeak)
    {
        var peakConcurrency = 0;
        var running = 0;
        var entered = 0;
        var sync = new object();
        var saturated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var phases = Enumerable.Range(0, 11)
            .Select(_ => (Func<CancellationToken, Task>)(async cancellationToken =>
            {
                lock (sync)
                {
                    running++;
                    peakConcurrency = Math.Max(peakConcurrency, running);
                }

                if (Interlocked.Increment(ref entered) == maxConcurrency)
                {
                    saturated.TrySetResult();
                }

                try
                {
                    // Hold the slot until the gate is full. A fixed delay lets early phases
                    // finish on a busy runner before the rest are scheduled, so the peak
                    // never reaches the configured concurrency.
                    await saturated.Task.WaitAsync(cancellationToken);
                }
                finally
                {
                    lock (sync)
                    {
                        running--;
                    }
                }
            }))
            .ToList();

        await InsightsV3RepositoryPhaseScheduler
            .RunAllAsync(maxConcurrency, phases, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(expectedPeak, peakConcurrency);
    }

    [Fact]
    public async Task RunAllAsyncCancellationWhileWaitingForGatePropagates()
    {
        var phaseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdFirstPhase = new SemaphoreSlim(0, 1);

        var phases = new List<Func<CancellationToken, Task>>
        {
            async cancellationToken =>
            {
                phaseStarted.SetResult();
                await holdFirstPhase.WaitAsync(cancellationToken);
            },
            _ => Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None),
        };

        using var cancellationTokenSource = new CancellationTokenSource();
        var runTask = InsightsV3RepositoryPhaseScheduler.RunAllAsync(1, phases, cancellationTokenSource.Token);

        await phaseStarted.Task;
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task RunAllAsyncReleasesGateWhenPhaseFails()
    {
        var peakConcurrency = 0;
        var running = 0;
        var sync = new object();
        var secondPhaseStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var phases = new List<Func<CancellationToken, Task>>
        {
            _ => throw new InvalidOperationException("phase failed"),
            async cancellationToken =>
            {
                lock (sync)
                {
                    running++;
                    peakConcurrency = Math.Max(peakConcurrency, running);
                }

                secondPhaseStarted.SetResult();

                try
                {
                    await Task.Delay(20, cancellationToken);
                }
                finally
                {
                    lock (sync)
                    {
                        running--;
                    }
                }
            },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InsightsV3RepositoryPhaseScheduler.RunAllAsync(1, phases, CancellationToken.None));

        await secondPhaseStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, peakConcurrency);
    }
}
