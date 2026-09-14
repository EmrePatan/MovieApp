using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Models.HotRelease;

namespace MovieApp.Application.Services.HotRelease;

public sealed class HotReleaseCheckService(
    IHotReleaseCandidateRepository candidateRepository,
    IHotReleaseCandidateProcessor candidateProcessor) : IHotReleaseCheckService
{
    public async Task<HotReleaseCheckResult> RunAsync(
        DateOnly boundaryDate,
        CancellationToken cancellationToken = default)
    {
        var candidates = await candidateRepository.GetCandidatesAsync(boundaryDate, cancellationToken);

        var checkedCount = 0;
        var hydratedCount = 0;
        var releaseEventsCreated = 0;
        var failures = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                var result = await candidateProcessor.ProcessAsync(candidate, boundaryDate, cancellationToken);
                checkedCount++;
                if (result.Hydrated)
                {
                    hydratedCount++;
                }

                releaseEventsCreated += result.ReleaseEventsCreated;
            }
            catch (Exception ex) when (ex is InvalidOperationException or NotFoundException)
            {
                failures++;
            }
        }

        return new HotReleaseCheckResult(
            candidates.Count,
            checkedCount,
            hydratedCount,
            releaseEventsCreated,
            failures);
    }
}
