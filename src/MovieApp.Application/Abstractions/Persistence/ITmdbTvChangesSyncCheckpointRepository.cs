using MovieApp.Domain.Entities;

namespace MovieApp.Application.Abstractions.Persistence;

public interface ITmdbTvChangesSyncCheckpointRepository
{
    /// <summary>Failed titles that still have retries left, the longest waiting first.</summary>
    Task<IReadOnlyList<TmdbChangesFailedTarget>> GetRetryableFailuresAsync(
        string checkpointKey,
        int maxRetries,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountExhaustedFailuresAsync(
        string checkpointKey,
        int maxRetries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates failure rows. A newer change gives an exhausted title its retries back.
    /// </summary>
    Task RecordFailuresAsync(
        string checkpointKey,
        IReadOnlyCollection<Guid> targetIds,
        DateOnly changeDate,
        int maxRetries,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default);

    Task RecordRetryFailureAsync(
        string checkpointKey,
        Guid targetId,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default);

    Task ResolveFailuresAsync(
        string checkpointKey,
        IReadOnlyCollection<Guid> targetIds,
        CancellationToken cancellationToken = default);

    Task<DateOnly?> GetLastCompletedEndDateAsync(
        string checkpointKey,
        CancellationToken cancellationToken = default);

    Task CompleteWindowAsync(
        string checkpointKey,
        DateOnly completedEndDate,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default);
}
