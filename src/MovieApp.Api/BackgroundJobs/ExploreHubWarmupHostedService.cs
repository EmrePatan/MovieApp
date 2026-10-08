using MovieApp.Application.Services.Search;

namespace MovieApp.Api.BackgroundJobs;

/// <summary>
/// Warms the shared Keşfet caches, including the eight genre rails, on startup and every
/// 30 minutes so the first hub open after a deploy or TTL expiry is a cache hit.
/// </summary>
public sealed class ExploreHubWarmupHostedService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<ExploreHubWarmupHostedService> logger) : IHostedService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private CancellationTokenSource? _stop;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return Task.CompletedTask;
        }

        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(() => RunAsync(_stop.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stop?.Cancel();
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                await WarmOnceAsync(cancellationToken);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task WarmOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await ExploreHubWarmup.WarmAsync(scope.ServiceProvider, cancellationToken);
            BackgroundJobLogMessages.LogExploreHubWarmupFinished(logger, ExploreHubWarmup.SectionSize);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            BackgroundJobLogMessages.LogExploreHubWarmupFailed(logger, exception);
        }
    }
}
