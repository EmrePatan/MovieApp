using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Services.ReleaseNotifications;

namespace MovieApp.UnitTests.ReleaseNotifications;

internal static class ReleaseNotificationFanoutServiceTestHelpers
{
    internal static readonly IMovieReleaseFollowCleanupService NoOpCleanup = new NoOpMovieReleaseFollowCleanupService();

    internal static ReleaseNotificationFanoutService CreateService(IReleaseNotificationFanoutRepository repository) =>
        new(repository, NoOpCleanup);

    private sealed class NoOpMovieReleaseFollowCleanupService : IMovieReleaseFollowCleanupService
    {
        public Task CleanupAsync(IReadOnlyCollection<Guid> movieIds, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
