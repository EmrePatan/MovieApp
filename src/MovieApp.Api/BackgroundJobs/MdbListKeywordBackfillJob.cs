using System.Diagnostics;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.Api.BackgroundJobs;

public sealed class MdbListKeywordBackfillJob(
    IMdbListKeywordBackfillService backfillService,
    IOptions<MdbListKeywordBackfillOptions> options,
    ILogger<MdbListKeywordBackfillJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 0)]
    public Task ExecuteAsync() =>
        BackgroundJobOperationalRunner.RunAsync(
            logger,
            RecurringJobIds.MdbListKeywordBackfill,
            async () =>
            {
                if (!options.Value.Enabled)
                {
                    BackgroundJobLogMessages.LogMdbListKeywordBackfillDisabled(logger);
                    return;
                }

                var batchSize = options.Value.BatchSize;
                var stopwatch = Stopwatch.StartNew();
                var candidates = await backfillService.SelectCandidatesAsync(batchSize, [], CancellationToken.None);
                if (candidates.Count == 0)
                {
                    BackgroundJobLogMessages.LogMdbListKeywordBackfillNoCandidates(logger);
                    return;
                }

                var result = await backfillService.ProcessBatchAsync(candidates, CancellationToken.None);
                stopwatch.Stop();

                BackgroundJobLogMessages.LogMdbListKeywordBackfillCompleted(
                    logger,
                    result.Selected,
                    result.Succeeded,
                    result.Failed,
                    result.Skipped,
                    result.MoviesProcessed,
                    result.TvShowsProcessed,
                    stopwatch.ElapsedMilliseconds);
            });
}
