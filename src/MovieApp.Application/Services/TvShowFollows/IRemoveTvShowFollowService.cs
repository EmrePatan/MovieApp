namespace MovieApp.Application.Services.TvShowFollows;

public interface IRemoveTvShowFollowService
{
    Task RemoveAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
