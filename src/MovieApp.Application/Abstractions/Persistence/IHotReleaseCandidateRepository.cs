using MovieApp.Application.Models.HotRelease;

namespace MovieApp.Application.Abstractions.Persistence;

public interface IHotReleaseCandidateRepository
{
    Task<IReadOnlyList<HotReleaseCandidate>> GetCandidatesAsync(
        DateOnly boundaryDate,
        CancellationToken cancellationToken = default);
}
