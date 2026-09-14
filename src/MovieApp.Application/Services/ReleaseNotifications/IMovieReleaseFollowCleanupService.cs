namespace MovieApp.Application.Services.ReleaseNotifications;

public interface IMovieReleaseFollowCleanupService
{
    Task CleanupAsync(
        IReadOnlyCollection<Guid> movieIds,
        CancellationToken cancellationToken = default);
}
