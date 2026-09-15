using Microsoft.Extensions.Logging;

namespace MovieApp.Api.BackgroundJobs;

internal static class BackgroundJobOperationalRunner
{
    internal static async Task RunAsync(ILogger logger, string jobId, Func<Task> executeAsync)
    {
        BackgroundJobLogMessages.LogBackgroundJobStarted(logger, jobId);

        try
        {
            await executeAsync();
        }
        catch (Exception exception)
        {
            BackgroundJobLogMessages.LogBackgroundJobFailed(logger, jobId, exception);
            throw;
        }
    }
}