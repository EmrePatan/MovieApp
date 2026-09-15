using Hangfire;

namespace MovieApp.Api.BackgroundJobs;

public sealed class CatalogKeywordBackfillJobEnqueuer(IBackgroundJobClient backgroundJobClient) : ICatalogKeywordBackfillJobEnqueuer
{
    public string EnqueueOneExecution() =>
        backgroundJobClient.Enqueue<CatalogKeywordBackfillJob>(job => job.ExecuteAsync());
}
