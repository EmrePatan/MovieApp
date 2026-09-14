using MovieApp.Application.Models.ReleaseNotifications;

namespace MovieApp.Application.Services.ReleaseNotifications;

public interface IReleaseNotificationFanoutService
{
    Task<ReleaseNotificationFanoutResult> ProcessAsync(
        IReadOnlyCollection<Guid> catalogReleaseEventIds,
        CancellationToken cancellationToken = default);
}
