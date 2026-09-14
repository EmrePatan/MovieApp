using MovieApp.Application.Models.ReleaseDetection;

namespace MovieApp.Application.Abstractions.ReleaseDetection;

public interface IReleaseDetector
{
    Task<ReleaseDetectionResult> ScanTvShowAsync(
        Guid tvShowId,
        ReleaseDetectionMode mode,
        DateOnly boundary,
        CancellationToken cancellationToken = default);
}
