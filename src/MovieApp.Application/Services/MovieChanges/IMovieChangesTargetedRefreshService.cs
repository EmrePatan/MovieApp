using MovieApp.Application.Models.Changes;

namespace MovieApp.Application.Services.MovieChanges;

public interface IMovieChangesTargetedRefreshService
{
    Task<TmdbChangesTargetRefreshResult> RefreshRelevantMovieAsync(
        Guid movieId,
        CancellationToken cancellationToken = default);
}
