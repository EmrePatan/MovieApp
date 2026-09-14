using Hangfire;
using Microsoft.Extensions.Logging;
using MovieApp.Application.Services.MovieRelease;

namespace MovieApp.Api.BackgroundJobs;

public sealed class MovieReleaseCheckJob(
    IMovieReleaseCheckService movieReleaseCheckService,
    ILogger<MovieReleaseCheckJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 1)]
    public async Task ExecuteAsync()
    {
        var result = await movieReleaseCheckService.RunAsync();

        BackgroundJobLogMessages.LogMovieReleaseCheckCompleted(
            logger,
            result.MoviesChecked,
            result.ReleaseEventsCreated,
            result.SkippedProviderFailures,
            result.SkippedNotReleased);
    }
}
