using MovieApp.Application.Models.HotRelease;

namespace MovieApp.Application.Services.HotRelease;

public interface IHotReleaseCandidateProcessor
{
    Task<HotReleaseCandidateProcessResult> ProcessAsync(
        HotReleaseCandidate candidate,
        DateOnly boundaryDate,
        CancellationToken cancellationToken = default);
}

public sealed record HotReleaseCandidateProcessResult(
    bool Hydrated,
    int ReleaseEventsCreated);
