namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class InsightsV3RepositoryPhaseScheduler
{
    internal static async Task RunAllAsync(
        int maxRepositoryConcurrency,
        IReadOnlyList<Func<CancellationToken, Task>> phases,
        CancellationToken cancellationToken)
    {
        if (phases.Count == 0)
        {
            return;
        }

        using var gate = new SemaphoreSlim(maxRepositoryConcurrency, maxRepositoryConcurrency);
        var tasks = phases.Select(phase => RunPhaseAsync(gate, phase, cancellationToken)).ToArray();
        await Task.WhenAll(tasks);
    }

    internal static Task<T> RunBoundedAsync<T>(
        SemaphoreSlim gate,
        Func<CancellationToken, Task<T>> phase,
        CancellationToken cancellationToken) =>
        RunReturningPhaseAsync(gate, phase, cancellationToken);

    private static async Task RunPhaseAsync(
        SemaphoreSlim gate,
        Func<CancellationToken, Task> phase,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await phase(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<T> RunReturningPhaseAsync<T>(
        SemaphoreSlim gate,
        Func<CancellationToken, Task<T>> phase,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await phase(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
