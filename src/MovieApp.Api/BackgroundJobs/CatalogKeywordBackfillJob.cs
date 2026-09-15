using System.Diagnostics;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieApp.Application.Configuration;
using MovieApp.Application.Services.Keywords;

namespace MovieApp.Api.BackgroundJobs;

public sealed class CatalogKeywordBackfillJob(
    ICatalogKeywordBackfillService backfillService,
    IOptions<CatalogKeywordBackfillOptions> options,
    ILogger<CatalogKeywordBackfillJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task ExecuteAsync()
    {
        var batchSize = options.Value.BatchSize;
        var coverageBefore = await backfillService.GetCoverageAsync(CancellationToken.None);
        var stopwatch = Stopwatch.StartNew();

        var candidates = await backfillService.SelectCandidatesAsync(batchSize, CancellationToken.None);
        if (candidates.Count == 0)
        {
            BackgroundJobLogMessages.LogCatalogKeywordBackfillNoCandidates(
                logger,
                coverageBefore.OverallSynced,
                coverageBefore.OverallEligible,
                coverageBefore.OverallCoveragePercent);
            return;
        }

        var result = await backfillService.ProcessBatchAsync(candidates, CancellationToken.None);
        stopwatch.Stop();

        var coverageAfter = await backfillService.GetCoverageAsync(CancellationToken.None);

        BackgroundJobLogMessages.LogCatalogKeywordBackfillCompleted(
            logger,
            result.Selected,
            result.Succeeded,
            result.Failed,
            result.Skipped,
            result.MoviesProcessed,
            result.TvShowsProcessed,
            stopwatch.ElapsedMilliseconds,
            coverageBefore.OverallCoveragePercent,
            coverageAfter.OverallCoveragePercent);
    }
}
