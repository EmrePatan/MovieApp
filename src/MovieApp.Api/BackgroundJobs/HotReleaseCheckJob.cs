using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.HotRelease;

namespace MovieApp.Api.BackgroundJobs;

public sealed class HotReleaseCheckJob(IHotReleaseCheckService hotReleaseCheckService, ILogger<HotReleaseCheckJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 1)]
    public async Task ExecuteAsync()
    {
        var boundaryDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await hotReleaseCheckService.RunAsync(boundaryDate);

        logger.LogInformation(
            "Hot release check completed: boundary={BoundaryDate} candidates={Candidates} checked={Checked} hydrated={Hydrated} events={Events} failures={Failures}",
            boundaryDate,
            result.Candidates,
            result.Checked,
            result.Hydrated,
            result.ReleaseEventsCreated,
            result.Failures);
    }
}
