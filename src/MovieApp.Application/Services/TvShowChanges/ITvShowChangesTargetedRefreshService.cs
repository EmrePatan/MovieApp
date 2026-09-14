namespace MovieApp.Application.Services.TvShowChanges;

public interface ITvShowChangesTargetedRefreshService
{
    Task RefreshFollowedShowAsync(
        Guid tvShowId,
        DateOnly boundaryDate,
        DateOnly changeSignalDate,
        CancellationToken cancellationToken = default);
}
