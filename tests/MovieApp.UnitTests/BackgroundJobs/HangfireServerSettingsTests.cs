using MovieApp.Api.BackgroundJobs;

namespace MovieApp.UnitTests.BackgroundJobs;

public sealed class HangfireServerSettingsTests
{
    [Fact]
    public void WorkerCountRemainsTwo()
    {
        Assert.Equal(2, HangfireServerSettings.WorkerCount);
    }

    [Fact]
    public void QueuesIncludeDefaultTvFollowBaselineAndExternalRatings()
    {
        Assert.Equal(
            new[]
            {
                "default",
                TvShowFollowBaselineJob.QueueName,
                ExternalRatingsRefreshJob.QueueName,
            },
            HangfireServerSettings.Queues);
    }

    [Fact]
    public void ExternalRatingsQueueUsesJobConstant()
    {
        Assert.Equal(ExternalRatingsRefreshJob.QueueName, "external-ratings");
        Assert.Contains(ExternalRatingsRefreshJob.QueueName, HangfireServerSettings.Queues);
    }
}
