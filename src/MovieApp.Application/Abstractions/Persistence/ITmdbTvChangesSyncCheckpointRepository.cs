namespace MovieApp.Application.Abstractions.Persistence;

public interface ITmdbTvChangesSyncCheckpointRepository
{
    Task<DateOnly?> GetLastCompletedEndDateAsync(
        string checkpointKey,
        CancellationToken cancellationToken = default);

    Task CompleteWindowAsync(
        string checkpointKey,
        DateOnly completedEndDate,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default);
}
