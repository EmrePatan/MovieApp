using MovieApp.Application.Models.ReleaseDetection;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.ReleaseDetection;

public interface IReleaseDetector
{
    Task<ReleaseDetectionResult> ScanTvShowAsync(
        Guid tvShowId,
        ReleaseDetectionMode mode,
        DateOnly boundary,
        IReadOnlyList<Season>? seasons = null,
        CancellationToken cancellationToken = default);
}
