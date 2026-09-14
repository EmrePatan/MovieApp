using MovieApp.Application.Models.HotRelease;

namespace MovieApp.Application.Services.HotRelease;

public interface IHotReleaseCheckService
{
    Task<HotReleaseCheckResult> RunAsync(
        DateOnly boundaryDate,
        CancellationToken cancellationToken = default);
}
