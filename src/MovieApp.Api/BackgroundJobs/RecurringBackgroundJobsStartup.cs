using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;

namespace MovieApp.Api.BackgroundJobs;

public sealed class RecurringBackgroundJobsStartup(
    IRecurringBackgroundJobRegistrar registrar,
    IOptions<BackgroundJobsOptions> backgroundJobsOptions,
    ILogger<RecurringBackgroundJobsStartup> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (backgroundJobsOptions.Value.Enabled)
        {
            registrar.RegisterRecurringJobs();
        }
        else
        {
            BackgroundJobLogMessages.LogBackgroundJobsDisabled(logger);
            registrar.RemoveAllRecurringJobs();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
