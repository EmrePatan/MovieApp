using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.MovieChanges;

internal sealed class FakeCheckpointRepository : ITmdbTvChangesSyncCheckpointRepository
{
    public DateOnly? LastCompletedEndDate { get; set; }

    public List<DateOnly> CompletedWindows { get; } = [];

    public Dictionary<Guid, TmdbChangesFailedTarget> Failures { get; } = [];

    public Task<DateOnly?> GetLastCompletedEndDateAsync(
        string checkpointKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(LastCompletedEndDate);

    public Task CompleteWindowAsync(
        string checkpointKey,
        DateOnly completedEndDate,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        LastCompletedEndDate = completedEndDate;
        CompletedWindows.Add(completedEndDate);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TmdbChangesFailedTarget>> GetRetryableFailuresAsync(
        string checkpointKey,
        int maxRetries,
        int take,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TmdbChangesFailedTarget>>(
            Failures.Values.Where(failure => failure.RetryCount < maxRetries).Take(take).ToList());

    public Task<int> CountExhaustedFailuresAsync(
        string checkpointKey,
        int maxRetries,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Failures.Values.Count(failure => failure.RetryCount >= maxRetries));

    public Task RecordFailuresAsync(
        string checkpointKey,
        IReadOnlyCollection<Guid> targetIds,
        DateOnly changeDate,
        int maxRetries,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default)
    {
        foreach (var targetId in targetIds)
        {
            Failures.TryAdd(targetId, new TmdbChangesFailedTarget
            {
                CheckpointKey = checkpointKey,
                TargetId = targetId,
                ChangeDate = changeDate,
                LastFailedAtUtc = failedAtUtc
            });
        }

        return Task.CompletedTask;
    }

    public Task RecordRetryFailureAsync(
        string checkpointKey,
        Guid targetId,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default)
    {
        Failures[targetId].RetryCount++;
        return Task.CompletedTask;
    }

    public Task ResolveFailuresAsync(
        string checkpointKey,
        IReadOnlyCollection<Guid> targetIds,
        CancellationToken cancellationToken = default)
    {
        foreach (var targetId in targetIds)
        {
            Failures.Remove(targetId);
        }

        return Task.CompletedTask;
    }
}
