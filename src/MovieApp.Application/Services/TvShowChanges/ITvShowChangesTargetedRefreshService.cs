using MovieApp.Application.Models.Changes;

namespace MovieApp.Application.Services.TvShowChanges;

public interface ITvShowChangesTargetedRefreshService
{
    Task<TmdbChangesTargetRefreshResult> RefreshRelevantShowAsync(
        Guid tvShowId,
        DateOnly boundaryDate,
        DateOnly changeSignalDate,
        CancellationToken cancellationToken = default);
}
