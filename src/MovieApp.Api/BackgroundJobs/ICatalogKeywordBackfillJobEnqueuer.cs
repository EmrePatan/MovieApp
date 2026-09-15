namespace MovieApp.Api.BackgroundJobs;

public interface ICatalogKeywordBackfillJobEnqueuer
{
    string EnqueueOneExecution();
}
