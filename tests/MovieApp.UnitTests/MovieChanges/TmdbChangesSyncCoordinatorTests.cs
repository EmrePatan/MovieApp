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
    public async Task SyncAsync_DefersABrokenTitleAndAdvancesTheCheckpoint()
    {
        var targets = Targets(1);
        var checkpoint = YesterdayCheckpoint();

        var result = await RunAsync(targets, checkpoint, _ => TmdbChangesTargetRefreshOutcome.Failed);

        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.WindowsProcessed);
        Assert.Equal(targets.Values.Single(), Assert.Single(checkpoint.Failures).Key);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(30)]
    public async Task SyncAsync_KeepsTheWindow_WhenEveryRefreshFails(int targetCount)
    {
        var checkpoint = YesterdayCheckpoint();

        var result = await RunAsync(
            Targets(targetCount),
            checkpoint,
            _ => TmdbChangesTargetRefreshOutcome.Failed);

        Assert.Equal(0, result.WindowsProcessed);
        Assert.Empty(checkpoint.CompletedWindows);
        Assert.Empty(checkpoint.Failures);
    }

    [Fact]
    public async Task SyncAsync_StopsRetryingATitleAfterTheLimit_AndKeepsItsRow()
    {
        var targets = Targets(1);
        var checkpoint = YesterdayCheckpoint();

        for (var run = 0; run < 10; run++)
        {
            await RunAsync(targets, checkpoint, _ => TmdbChangesTargetRefreshOutcome.Failed);
        }

        Assert.Equal(TmdbChangesSyncCoordinator.MaxRetries, Assert.Single(checkpoint.Failures).Value.RetryCount);
    }

    [Fact]
    public async Task SyncAsync_RemovesTheFailureRow_WhenARetryWorks()
    {
        var targets = Targets(1);
        var checkpoint = YesterdayCheckpoint();
        await RunAsync(targets, checkpoint, _ => TmdbChangesTargetRefreshOutcome.Failed);

        await RunAsync(targets, checkpoint, _ => TmdbChangesTargetRefreshOutcome.Refreshed);

        Assert.Empty(checkpoint.Failures);
    }

    private static readonly DateTime Instant = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    private static Dictionary<int, Guid> Targets(int count) =>
        Enumerable.Range(1, count).ToDictionary(id => id, _ => Guid.NewGuid());

    private static FakeCheckpointRepository YesterdayCheckpoint() =>
        new() { LastCompletedEndDate = DateOnly.FromDateTime(Instant).AddDays(-1) };

    private static Task<TmdbChangesSyncResult> RunAsync(
        Dictionary<int, Guid> targets,
        FakeCheckpointRepository checkpoint,
        Func<Guid, TmdbChangesTargetRefreshOutcome> refresh) =>
        TmdbChangesSyncCoordinator.SyncAsync(
            "movie",
            (_, _, page, _) => Task.FromResult(
                page == 1
                    ? new TmdbChangesPageResult(targets.Keys.ToArray(), 1, 1)
                    : new TmdbChangesPageResult([], page, 1)),
            (_, _) => Task.FromResult(CatalogChangesRefreshMaps.Create(targets, new Dictionary<int, Guid>())),
            (id, _, _, _) => Task.FromResult(new TmdbChangesTargetRefreshResult(refresh(id), [])),
            checkpoint,
            NullLogger.Instance,
            "movie",
            Instant);

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
}
