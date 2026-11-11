using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.Changes;

namespace MovieApp.UnitTests.MovieChanges;

public sealed class TmdbChangesSyncCoordinatorTests
{
    [Fact]
    public async Task SyncAsync_RefreshesUserRelevantOnly_WhenDiscoveryIrrelevant()
    {
        const int tmdbId = 101;
        var movieId = Guid.NewGuid();
        var refreshed = new List<Guid>();
        var maps = CatalogChangesRefreshMaps.Create(
            new Dictionary<int, Guid> { [tmdbId] = movieId },
            new Dictionary<int, Guid>());

        var result = await RunSingleIdSyncAsync(tmdbId, maps, refreshed);

        Assert.Equal(1, result.Refreshed);
        Assert.Equal(movieId, Assert.Single(refreshed));
    }

    [Fact]
    public async Task SyncAsync_RefreshesDiscoveryRelevant_WhenUserIrrelevant()
    {
        const int tmdbId = 202;
        var movieId = Guid.NewGuid();
        var refreshed = new List<Guid>();
        var maps = CatalogChangesRefreshMaps.Create(
            new Dictionary<int, Guid>(),
            new Dictionary<int, Guid> { [tmdbId] = movieId });

        var result = await RunSingleIdSyncAsync(tmdbId, maps, refreshed);

        Assert.Equal(1, result.DiscoveryOnlyRelevantMatches);
        Assert.Equal(1, result.Refreshed);
        Assert.Equal(movieId, Assert.Single(refreshed));
    }

    [Fact]
    public async Task SyncAsync_Skips_WhenNeitherUserNorDiscoveryRelevant()
    {
        const int tmdbId = 303;
        var refreshed = new List<Guid>();
        var maps = CatalogChangesRefreshMaps.Create(
            new Dictionary<int, Guid>(),
            new Dictionary<int, Guid>());

        var result = await RunSingleIdSyncAsync(tmdbId, maps, refreshed);

        Assert.Equal(0, result.Refreshed);
        Assert.Empty(refreshed);
    }

    [Fact]
    public async Task SyncAsync_RefreshesOnce_WhenBothUserAndDiscoveryRelevant()
    {
        const int tmdbId = 404;
        var movieId = Guid.NewGuid();
        var refreshed = new List<Guid>();
        var maps = CatalogChangesRefreshMaps.Create(
            new Dictionary<int, Guid> { [tmdbId] = movieId },
            new Dictionary<int, Guid> { [tmdbId] = movieId });

        var result = await RunSingleIdSyncAsync(tmdbId, maps, refreshed);

        Assert.Equal(1, result.Refreshed);
        Assert.Equal(movieId, Assert.Single(refreshed));
    }

    [Fact]
    public async Task SyncAsync_LoadsRelevantTargetsOnce_WhenMultipleChunks()
    {
        var syncInstant = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var targetDate = DateOnly.FromDateTime(syncInstant);
        var lastCompletedEndDate = targetDate.AddDays(-45);
        var result = await TmdbChangesSyncCoordinator.SyncAsync(
            "movie",
            (_, _, _, _) => Task.FromResult(new TmdbChangesPageResult([], 1, 1)),
            (_, _) => Task.FromResult(CatalogChangesRefreshMaps.Create(
                new Dictionary<int, Guid>(),
                new Dictionary<int, Guid>())),
            (_, _, _, _) => Task.FromResult(TmdbChangesTargetRefreshResult.Refreshed([])),
            new FakeCheckpointRepository { LastCompletedEndDate = lastCompletedEndDate },
            NullLogger.Instance,
            "movie",
            syncInstant);

        Assert.True(result.WindowsProcessed > 1);
    }

    [Fact]
    public async Task SyncAsync_DoesNotAdvanceCheckpoint_WhenAnyTargetRefreshFails()
    {
        const int tmdbId = 505;
        var syncInstant = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var targetDate = DateOnly.FromDateTime(syncInstant);
        var checkpoint = new FakeCheckpointRepository { LastCompletedEndDate = targetDate.AddDays(-1) };
        var maps = CatalogChangesRefreshMaps.Create(
            new Dictionary<int, Guid> { [tmdbId] = Guid.NewGuid() },
            new Dictionary<int, Guid>());

        var result = await TmdbChangesSyncCoordinator.SyncAsync(
            "movie",
            (_, _, page, _) => Task.FromResult(
                page == 1
                    ? new TmdbChangesPageResult([tmdbId], 1, 1)
                    : new TmdbChangesPageResult([], page, 1)),
            (_, _) => Task.FromResult(maps),
            (_, _, _, _) => Task.FromResult(new TmdbChangesTargetRefreshResult(
                TmdbChangesTargetRefreshOutcome.Failed,
                [])),
            checkpoint,
            NullLogger.Instance,
            "movie",
            syncInstant);

        Assert.Equal(1, result.Failed);
        Assert.Equal(0, result.WindowsProcessed);
        Assert.Empty(checkpoint.CompletedWindows);
    }

    [Fact]
    public async Task SyncAsync_BoundsTargetRefreshConcurrency()
    {
        var syncInstant = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var targetDate = DateOnly.FromDateTime(syncInstant);
        var userRelevant = Enumerable.Range(1, 8)
            .ToDictionary(id => id, _ => Guid.NewGuid());
        var maps = CatalogChangesRefreshMaps.Create(userRelevant, new Dictionary<int, Guid>());
        var active = 0;
        var peak = 0;

        var result = await TmdbChangesSyncCoordinator.SyncAsync(
            "movie",
            (_, _, page, _) => Task.FromResult(
                page == 1
                    ? new TmdbChangesPageResult(userRelevant.Keys.ToArray(), 1, 1)
                    : new TmdbChangesPageResult([], page, 1)),
            (_, _) => Task.FromResult(maps),
            async (_, _, _, token) =>
            {
                var current = Interlocked.Increment(ref active);
                int observed;
                do
                {
                    observed = Volatile.Read(ref peak);
                    if (observed >= current)
                    {
                        break;
                    }
                } while (Interlocked.CompareExchange(ref peak, current, observed) != observed);

                try
                {
                    await Task.Delay(30, token);
                    return TmdbChangesTargetRefreshResult.Refreshed([]);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            },
            new FakeCheckpointRepository { LastCompletedEndDate = targetDate.AddDays(-1) },
            NullLogger.Instance,
            "movie",
            syncInstant);

        Assert.Equal(8, result.Refreshed);
        Assert.Equal(2, peak);
    }

    private static async Task<TmdbChangesSyncResult> RunSingleIdSyncAsync(
        int tmdbId,
        CatalogChangesRefreshMaps maps,
        List<Guid> refreshed)
    {
        var syncInstant = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        var targetDate = DateOnly.FromDateTime(syncInstant);

        return await TmdbChangesSyncCoordinator.SyncAsync(
            "movie",
            (_, _, page, _) => Task.FromResult(
                page == 1
                    ? new TmdbChangesPageResult([tmdbId], 1, 1)
                    : new TmdbChangesPageResult([], page, 1)),
            (_, _) => Task.FromResult(maps),
            (id, _, _, _) =>
            {
                refreshed.Add(id);
                return Task.FromResult(TmdbChangesTargetRefreshResult.Refreshed([]));
            },
            new FakeCheckpointRepository { LastCompletedEndDate = targetDate.AddDays(-1) },
            NullLogger.Instance,
            "movie",
            syncInstant);
    }

    private sealed class FakeCheckpointRepository : ITmdbTvChangesSyncCheckpointRepository
    {
        public DateOnly? LastCompletedEndDate { get; init; }

        public List<DateOnly> CompletedWindows { get; } = [];

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
            CompletedWindows.Add(completedEndDate);
            return Task.CompletedTask;
        }
    }
}
