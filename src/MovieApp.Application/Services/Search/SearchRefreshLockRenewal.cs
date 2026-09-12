using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Configuration;

namespace MovieApp.Application.Services.Search;

internal static class SearchRefreshLockRenewal
{
    internal static async Task RunAsync(
        ISearchRefreshLockService lockService,
        SearchRefreshLockHandle handle,
        SearchOptions options,
        Action onOwnershipLost,
        CancellationToken cancellationToken)
    {
        var renewalInterval = options.ProviderRefreshLockRenewalInterval;
        if (renewalInterval <= TimeSpan.Zero)
        {
            renewalInterval = TimeSpan.FromSeconds(10);
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(renewalInterval, cancellationToken);

                var renewed = await lockService.TryRenewAsync(
                    handle.LockKey,
                    handle.LockToken,
                    handle.Backend,
                    options.ProviderRefreshLockDuration,
                    cancellationToken);

                if (!renewed)
                {
                    onOwnershipLost();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
