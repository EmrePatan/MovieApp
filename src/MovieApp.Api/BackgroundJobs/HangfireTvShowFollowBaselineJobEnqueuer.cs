using Hangfire;
using MovieApp.Application.Abstractions.TvShowFollows;

namespace MovieApp.Api.BackgroundJobs;

public sealed class HangfireTvShowFollowBaselineJobEnqueuer(IBackgroundJobClient backgroundJobClient)
    : ITvShowFollowBaselineJobEnqueuer
{
    public Task EnqueueAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        backgroundJobClient.Enqueue<TvShowFollowBaselineJob>(
            job => job.ExecuteAsync(userId, tvShowId));

        return Task.CompletedTask;
    }
}
