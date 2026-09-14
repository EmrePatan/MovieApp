using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.TvShowChanges;
using MovieApp.Application.Services.TvShowChanges;
using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.TvShowChanges;

public sealed class TmdbTvChangesSyncServiceTests
{
    private static readonly DateTime SyncInstant = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly TargetDate = new(2026, 9, 15);
    private static readonly DateOnly WindowStart = new(2026, 9, 14);
    private static readonly Guid FollowedShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const int FollowedTmdbId = 900101;
    private const int UnfollowedTmdbId = 555555;

    [Fact]
    public async Task SyncAsync_AggregatesAllPagesAndDeduplicatesIds()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages =
            [
                new TmdbTvChangesPageResult([FollowedTmdbId, UnfollowedTmdbId], 1, 2),
                new TmdbTvChangesPageResult([FollowedTmdbId, 777777], 2, 2)
            ]
        };

        var refreshService = new RecordingRefreshService();
        var service = CreateService(changesProvider, refreshService);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(2, changesProvider.Calls.Count);
        Assert.Equal(3, result.ChangedTmdbIdsObserved);
        Assert.Equal(1, result.FollowedShowsRefreshed);
        Assert.Equal(FollowedShowId, Assert.Single(refreshService.RefreshedTvShowIds));
    }

    [Fact]
    public async Task SyncAsync_UnfollowedChangedId_DoesNotRefresh()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbTvChangesPageResult([UnfollowedTmdbId], 1, 1)]
        };

        var refreshService = new RecordingRefreshService();
        var service = CreateService(changesProvider, refreshService);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(0, result.FollowedShowsRefreshed);
        Assert.Empty(refreshService.RefreshedTvShowIds);
    }

    [Fact]
    public async Task SyncAsync_MultipleFollowersSameShow_RefreshesOnce()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbTvChangesPageResult([FollowedTmdbId], 1, 1)]
        };

        var refreshService = new RecordingRefreshService();
        var followedRepository = new FakeFollowedTvShowCatalogRepository(
            new Dictionary<int, Guid> { [FollowedTmdbId] = FollowedShowId });

        var service = new TmdbTvChangesSyncService(
            changesProvider,
            new FakeCheckpointRepository(),
            followedRepository,
            refreshService);

        await service.SyncAsync(SyncInstant);

        Assert.Single(refreshService.RefreshedTvShowIds);
    }

    [Fact]
    public async Task SyncAsync_InvokesPostRefreshBoundaryUsingWindowEndDate()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbTvChangesPageResult([FollowedTmdbId], 1, 1)]
        };

        var refreshService = new RecordingRefreshService();
        var service = CreateService(changesProvider, refreshService);

        await service.SyncAsync(SyncInstant);

        Assert.Equal(TargetDate, Assert.Single(refreshService.BoundaryDates));
        Assert.Equal(TargetDate, Assert.Single(refreshService.ChangeSignalDates));
    }

    [Fact]
    public async Task SyncAsync_FailedChangesPage_DoesNotAdvanceCheckpoint()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbTvChangesPageResult([FollowedTmdbId], 1, 2)],
            FailOnPage = 2
        };

        var checkpointRepository = new FakeCheckpointRepository();
        var service = CreateService(changesProvider, new RecordingRefreshService(), checkpointRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncAsync(SyncInstant));

        Assert.Null(checkpointRepository.LastCompletedEndDate);
    }

    [Fact]
    public async Task SyncAsync_FailedShowRefresh_DoesNotAdvanceCheckpoint()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbTvChangesPageResult([FollowedTmdbId], 1, 1)]
        };

        var checkpointRepository = new FakeCheckpointRepository();
        var refreshService = new RecordingRefreshService { ShouldFail = true };
        var service = CreateService(changesProvider, refreshService, checkpointRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncAsync(SyncInstant));

        Assert.Null(checkpointRepository.LastCompletedEndDate);
    }

    [Fact]
    public async Task SyncAsync_SuccessfulWindow_AdvancesCheckpoint()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbTvChangesPageResult([], 1, 1)]
        };

        var checkpointRepository = new FakeCheckpointRepository();
        var service = CreateService(changesProvider, new RecordingRefreshService(), checkpointRepository);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(TargetDate, checkpointRepository.LastCompletedEndDate);
        Assert.Equal(TargetDate, result.LastCompletedEndDate);
    }

    [Fact]
    public async Task SyncAsync_RepeatedSameWindow_RemainsIdempotentForCheckpoint()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbTvChangesPageResult([], 1, 1)]
        };

        var checkpointRepository = new FakeCheckpointRepository();
        var service = CreateService(changesProvider, new RecordingRefreshService(), checkpointRepository);

        await service.SyncAsync(SyncInstant);
        var second = await service.SyncAsync(SyncInstant);

        Assert.Equal(1, second.WindowsProcessed);
        Assert.Equal(TargetDate, checkpointRepository.LastCompletedEndDate);
    }

    [Fact]
    public async Task SyncAsync_LongCatchUp_ProcessesChunksSequentiallyAndAdvancesToTarget()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            PagesByWindow =
            {
                [(new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 13))] =
                    [new TmdbTvChangesPageResult([], 1, 1)],
                [(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20))] =
                    [new TmdbTvChangesPageResult([], 1, 1)]
            }
        };

        var checkpointRepository = new FakeCheckpointRepository
        {
            LastCompletedEndDate = new DateOnly(2026, 8, 31)
        };

        var service = CreateService(
            changesProvider,
            new RecordingRefreshService(),
            checkpointRepository,
            targetDate: new DateOnly(2026, 9, 20));

        var result = await service.SyncAsync(new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, result.WindowsProcessed);
        Assert.Equal(new DateOnly(2026, 9, 20), checkpointRepository.LastCompletedEndDate);
    }

    private static TmdbTvChangesSyncService CreateService(
        FakeTmdbTvChangesProvider changesProvider,
        RecordingRefreshService refreshService,
        FakeCheckpointRepository? checkpointRepository = null,
        DateOnly? targetDate = null) =>
        new(
            changesProvider,
            checkpointRepository ?? new FakeCheckpointRepository(),
            new FakeFollowedTvShowCatalogRepository(
                new Dictionary<int, Guid> { [FollowedTmdbId] = FollowedShowId }),
            refreshService);

    private sealed class FakeTmdbTvChangesProvider : ITmdbTvChangesProvider
    {
        public List<TmdbTvChangesPageResult> Pages { get; init; } = [];

        public Dictionary<(DateOnly Start, DateOnly End), List<TmdbTvChangesPageResult>> PagesByWindow { get; init; } =
            new();

        public int? FailOnPage { get; init; }

        public List<(DateOnly Start, DateOnly End, int Page)> Calls { get; } = [];

        public Task<TmdbTvChangesPageResult> GetTvChangesPageAsync(
            DateOnly startDate,
            DateOnly endDate,
            int page,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((startDate, endDate, page));

            if (FailOnPage == page)
            {
                throw new InvalidOperationException("Simulated changes page failure.");
            }

            if (PagesByWindow.TryGetValue((startDate, endDate), out var windowPages))
            {
                return Task.FromResult(windowPages[page - 1]);
            }

            return Task.FromResult(Pages[page - 1]);
        }
    }

    private sealed class FakeCheckpointRepository : ITmdbTvChangesSyncCheckpointRepository
    {
        public DateOnly? LastCompletedEndDate { get; set; }

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
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFollowedTvShowCatalogRepository(IReadOnlyDictionary<int, Guid> followedShows)
        : IFollowedTvShowCatalogRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> GetFollowedTvShowIdsByTmdbIdAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(followedShows);
    }

    private sealed class RecordingRefreshService : ITvShowChangesTargetedRefreshService
    {
        public bool ShouldFail { get; init; }

        public List<Guid> RefreshedTvShowIds { get; } = [];

        public List<DateOnly> BoundaryDates { get; } = [];

        public List<DateOnly> ChangeSignalDates { get; } = [];

        public Task RefreshFollowedShowAsync(
            Guid tvShowId,
            DateOnly boundaryDate,
            DateOnly changeSignalDate,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                throw new InvalidOperationException("Simulated refresh failure.");
            }

            RefreshedTvShowIds.Add(tvShowId);
            BoundaryDates.Add(boundaryDate);
            ChangeSignalDates.Add(changeSignalDate);
            return Task.CompletedTask;
        }
    }
}
