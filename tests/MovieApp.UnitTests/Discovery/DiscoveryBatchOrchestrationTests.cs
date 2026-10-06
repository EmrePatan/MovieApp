using MovieApp.Application.Services.Search;

namespace MovieApp.UnitTests.Discovery;

public sealed class DiscoveryBatchOrchestrationTests
{
    [Fact]
    public async Task ExecuteInOrderAsync_DoesNotExceedConfiguredConcurrency()
    {
        const int maxConcurrency = 2;
        var items = Enumerable.Range(1, 8).ToList();
        var active = 0;
        var peak = 0;
        var gate = new SemaphoreSlim(1, 1);

        var results = await DiscoveryBatchOrchestration.ExecuteInOrderAsync(
            items,
            maxConcurrency,
            async (_, cancellationToken) =>
            {
                await gate.WaitAsync(cancellationToken);
                var current = Interlocked.Increment(ref active);
                peak = Math.Max(peak, current);
                gate.Release();

                await Task.Delay(25, cancellationToken);

                await gate.WaitAsync(cancellationToken);
                Interlocked.Decrement(ref active);
                gate.Release();

                return 0;
            },
            CancellationToken.None);

        Assert.Equal(items.Count, results.Count);
        Assert.True(peak <= maxConcurrency);
        Assert.Equal(0, active);
    }

    [Fact]
    public async Task ExecuteInOrderAsync_PreservesResultOrder()
    {
        var items = Enumerable.Range(1, 5).ToList();

        var results = await DiscoveryBatchOrchestration.ExecuteInOrderAsync(
            items,
            DiscoveryBatchOrchestration.DefaultMaxConcurrentOperations,
            (item, _) => Task.FromResult(item * 10),
            CancellationToken.None);

        Assert.Equal([10, 20, 30, 40, 50], results);
    }
}
