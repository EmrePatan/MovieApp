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

    internal static bool ShouldAdvanceCheckpoint(DateOnly? existingEndDate, DateOnly incomingEndDate) =>
        !existingEndDate.HasValue || incomingEndDate >= existingEndDate.Value;
}
