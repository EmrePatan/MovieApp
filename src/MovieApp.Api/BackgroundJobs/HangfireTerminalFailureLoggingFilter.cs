using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;

namespace MovieApp.Api.BackgroundJobs;

internal sealed class HangfireTerminalFailureLoggingFilter(
    ILogger<HangfireTerminalFailureLoggingFilter> logger) : IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState failedState)
        {
            return;
        }

        var job = context.BackgroundJob?.Job;
        var jobName = job is null
            ? "unknown"
            : $"{job.Type.Name}.{job.Method.Name}";

        var retryCount = context.GetJobParameter<int>("RetryCount");

        BackgroundJobLogMessages.LogBackgroundJobTerminalFailure(
            logger,
            context.BackgroundJob?.Id ?? "unknown",
            jobName,
            retryCount,
            failedState.Exception);
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }
}
