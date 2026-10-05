namespace MovieApp.Api.BackgroundJobs;

internal static class HangfireServerSettings
{
    public const int WorkerCount = 2;

    public static readonly string[] Queues =
    [
        "default",
        TvShowFollowBaselineJob.QueueName,
        ExternalRatingsRefreshJob.QueueName,
    ];
}
