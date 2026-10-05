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
