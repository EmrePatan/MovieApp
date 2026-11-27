using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Domain.Entities;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class TmdbTvChangesSyncCheckpointRepository(ApplicationDbContext dbContext)
    : ITmdbTvChangesSyncCheckpointRepository
{
    private const int MaxUpsertAttempts = 3;

    public async Task<DateOnly?> GetLastCompletedEndDateAsync(
        string checkpointKey,
        CancellationToken cancellationToken = default)
    {
        var checkpoint = await dbContext.TmdbTvChangesSyncCheckpoints
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.CheckpointKey == checkpointKey, cancellationToken);

        return checkpoint?.LastCompletedEndDate;
    }

    public async Task CompleteWindowAsync(
        string checkpointKey,
        DateOnly completedEndDate,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= MaxUpsertAttempts; attempt++)
        {
            var checkpoint = await dbContext.TmdbTvChangesSyncCheckpoints
                .FirstOrDefaultAsync(item => item.CheckpointKey == checkpointKey, cancellationToken);

            if (checkpoint is null)
            {
                checkpoint = new TmdbTvChangesSyncCheckpoint
                {
                    CheckpointKey = checkpointKey,
                    LastCompletedEndDate = completedEndDate,
                    LastCompletedAtUtc = completedAtUtc,
                    UpdatedAtUtc = completedAtUtc
                };

                dbContext.TmdbTvChangesSyncCheckpoints.Add(checkpoint);
            }
            else if (!ShouldAdvanceCheckpoint(checkpoint.LastCompletedEndDate, completedEndDate))
            {
                return;
            }
            else
            {
                checkpoint.LastCompletedEndDate = completedEndDate;
                checkpoint.LastCompletedAtUtc = completedAtUtc;
                checkpoint.UpdatedAtUtc = completedAtUtc;
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt < MaxUpsertAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException(
            $"Failed to upsert TMDB TV changes sync checkpoint '{checkpointKey}'.");
    }

    public async Task<IReadOnlyList<TmdbChangesFailedTarget>> GetRetryableFailuresAsync(
        string checkpointKey,
        int maxRetries,
        int take,
        CancellationToken cancellationToken = default) =>
        await dbContext.TmdbChangesFailedTargets
            .AsNoTracking()
            .Where(target => target.CheckpointKey == checkpointKey && target.RetryCount < maxRetries)
            .OrderBy(target => target.LastFailedAtUtc)
            .ThenBy(target => target.TargetId)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<int> CountExhaustedFailuresAsync(
        string checkpointKey,
        int maxRetries,
        CancellationToken cancellationToken = default) =>
        dbContext.TmdbChangesFailedTargets
            .CountAsync(
                target => target.CheckpointKey == checkpointKey && target.RetryCount >= maxRetries,
                cancellationToken);

    public async Task RecordFailuresAsync(
        string checkpointKey,
        IReadOnlyCollection<Guid> targetIds,
        DateOnly changeDate,
        int maxRetries,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (targetIds.Count == 0)
        {
            return;
        }

        var existing = await dbContext.TmdbChangesFailedTargets
            .Where(target => target.CheckpointKey == checkpointKey && targetIds.Contains(target.TargetId))
            .ToDictionaryAsync(target => target.TargetId, cancellationToken);

        foreach (var targetId in targetIds)
        {
            if (!existing.TryGetValue(targetId, out var row))
            {
                dbContext.TmdbChangesFailedTargets.Add(new TmdbChangesFailedTarget
                {
                    CheckpointKey = checkpointKey,
                    TargetId = targetId,
                    ChangeDate = changeDate,
                    LastFailedAtUtc = failedAtUtc
                });
                continue;
            }

            if (changeDate > row.ChangeDate)
            {
                if (row.RetryCount >= maxRetries)
                {
                    row.RetryCount = 0;
                }

                row.ChangeDate = changeDate;
            }

            row.LastFailedAtUtc = failedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordRetryFailureAsync(
        string checkpointKey,
        Guid targetId,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.TmdbChangesFailedTargets.FirstOrDefaultAsync(
            target => target.CheckpointKey == checkpointKey && target.TargetId == targetId,
            cancellationToken);
        if (row is null)
        {
            return;
        }

        row.RetryCount++;
        row.LastFailedAtUtc = failedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveFailuresAsync(
        string checkpointKey,
        IReadOnlyCollection<Guid> targetIds,
        CancellationToken cancellationToken = default)
    {
        if (targetIds.Count == 0)
        {
            return;
        }

        var rows = await dbContext.TmdbChangesFailedTargets
            .Where(target => target.CheckpointKey == checkpointKey && targetIds.Contains(target.TargetId))
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return;
        }

        dbContext.TmdbChangesFailedTargets.RemoveRange(rows);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static bool ShouldAdvanceCheckpoint(DateOnly? existingEndDate, DateOnly incomingEndDate) =>
        !existingEndDate.HasValue || incomingEndDate >= existingEndDate.Value;
}
