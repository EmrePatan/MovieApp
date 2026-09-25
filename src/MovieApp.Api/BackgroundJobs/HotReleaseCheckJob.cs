using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.HotRelease;

namespace MovieApp.Api.BackgroundJobs;

public sealed class HotReleaseCheckJob(IHotReleaseCheckService hotReleaseCheckService, ILogger<HotReleaseCheckJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 1)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.HotRelease,
            async () =>
            {
                var boundaryDate = DateOnly.FromDateTime(DateTime.UtcNow);
                var result = await hotReleaseCheckService.RunAsync(boundaryDate);

                BackgroundJobLogMessages.LogHotReleaseCheckCompleted(
                    logger,
                    boundaryDate,
                    result.Candidates,
                    result.Checked,
                    result.Hydrated,
                    result.ReleaseEventsCreated,
                    result.Failures);
            });
}
