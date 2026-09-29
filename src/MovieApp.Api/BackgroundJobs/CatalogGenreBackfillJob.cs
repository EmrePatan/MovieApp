using System.Diagnostics;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Catalog;

namespace MovieApp.Api.BackgroundJobs;

public sealed class CatalogGenreBackfillJob(
    ICatalogGenreBackfillService backfillService,
    IOptions<CatalogGenreBackfillOptions> options,
    ILogger<CatalogGenreBackfillJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 0)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.CatalogGenreBackfill,
            async () =>
            {
                if (!options.Value.Enabled)
                {
                    BackgroundJobLogMessages.LogCatalogGenreBackfillDisabled(logger);
                    return;
                }

                var coverageBefore = await backfillService.GetCoverageAsync(CancellationToken.None);
                BackgroundJobLogMessages.LogCatalogGenreBackfillStarted(
                    logger,
                    coverageBefore.MovieGenreLess,
                    coverageBefore.TvGenreLess,
                    coverageBefore.OverallRemaining);

                var stopwatch = Stopwatch.StartNew();
                var result = await backfillService.RunAsync(CancellationToken.None);
                stopwatch.Stop();

                if (result.BatchesProcessed == 0 && result.Selected == 0)
                {
                    BackgroundJobLogMessages.LogCatalogGenreBackfillNoCandidates(
                        logger,
                        coverageBefore.OverallRemaining);
                    return;
                }

                BackgroundJobLogMessages.LogCatalogGenreBackfillCompleted(
                    logger,
                    result.BatchesProcessed,
                    result.Selected,
                    result.Succeeded,
                    result.Unrepairable,
                    result.Failed,
                    result.Skipped,
                    result.MoviesProcessed,
                    result.TvShowsProcessed,
                    result.RemainingAfterRun,
                    stopwatch.ElapsedMilliseconds);
            });
}
