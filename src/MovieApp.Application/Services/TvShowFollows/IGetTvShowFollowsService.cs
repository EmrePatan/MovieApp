using MovieApp.Application.Models.TvShowFollows;

namespace MovieApp.Application.Services.TvShowFollows;

public interface IGetTvShowFollowsService
{
    Task<TvShowFollowsListResult> GetAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
