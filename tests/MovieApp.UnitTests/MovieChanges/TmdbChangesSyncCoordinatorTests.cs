using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.Changes;

namespace MovieApp.UnitTests.MovieChanges;

public sealed class TmdbChangesSyncCoordinatorTests
{
    [Fact]
    public async Task SyncAsync_LoadsRelevantTargetsOnce_WhenMultipleChunks()
    {
        var syncInstant = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var targetDate = DateOnly.FromDateTime(syncInstant);
        var lastCompletedEndDate = targetDate.AddDays(-45);
        var relevanceCalls = 0;

        var result = await TmdbChangesSyncCoordinator.SyncAsync(
            "movie",
            (_, _, _, _) => Task.FromResult(new TmdbChangesPageResult([], 1, 1)),
            _ =>
            {
                relevanceCalls++;
                return Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());
            },
            (_, _, _, _) => Task.FromResult(TmdbChangesTargetRefreshResult.Refreshed([])),
            new FakeCheckpointRepository { LastCompletedEndDate = lastCompletedEndDate },
            NullLogger.Instance,
            "movie",
            syncInstant);

        Assert.True(result.WindowsProcessed > 1);
        Assert.Equal(1, relevanceCalls);
    }

    private sealed class FakeCheckpointRepository : ITmdbTvChangesSyncCheckpointRepository
    {
        public DateOnly? LastCompletedEndDate { get; init; }

        public Task<DateOnly?> GetLastCompletedEndDateAsync(
            string checkpointKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(LastCompletedEndDate);

        public Task CompleteWindowAsync(
            string checkpointKey,
            DateOnly completedEndDate,
            DateTime completedAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
