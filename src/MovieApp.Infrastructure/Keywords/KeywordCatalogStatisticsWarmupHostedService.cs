using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Abstractions.Keywords;
using MovieApp.Application.Configuration;

namespace MovieApp.Infrastructure.Keywords;

public sealed class KeywordCatalogStatisticsWarmupHostedService(
    IKeywordCatalogStatisticsRefreshService refreshService,
    IOptions<KeywordCatalogStatisticsOptions> options,
    ILogger<KeywordCatalogStatisticsWarmupHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || !options.Value.RefreshOnStartup)
        {
            return Task.CompletedTask;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await refreshService.RefreshAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                KeywordCatalogStatisticsLogMessages.LogStartupRefreshFailed(logger, exception);
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
