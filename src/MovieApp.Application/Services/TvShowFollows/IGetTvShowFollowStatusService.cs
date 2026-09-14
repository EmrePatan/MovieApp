using MovieApp.Application.Models.TvShowFollows;

namespace MovieApp.Application.Services.TvShowFollows;

public interface IGetTvShowFollowStatusService
{
    Task<TvShowFollowStatusResult> GetAsync(Guid tvShowId, CancellationToken cancellationToken = default);
}
