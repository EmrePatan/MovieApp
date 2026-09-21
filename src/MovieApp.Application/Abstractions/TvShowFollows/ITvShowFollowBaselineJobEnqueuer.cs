namespace MovieApp.Application.Abstractions.TvShowFollows;

public interface ITvShowFollowBaselineJobEnqueuer
{
    Task EnqueueAsync(Guid userId, Guid tvShowId, CancellationToken cancellationToken = default);
}
