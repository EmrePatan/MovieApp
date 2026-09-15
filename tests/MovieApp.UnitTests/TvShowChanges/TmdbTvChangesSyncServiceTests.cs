using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.TvShowChanges;

namespace MovieApp.UnitTests.TvShowChanges;

public sealed class TmdbTvChangesSyncServiceTests
{
    private static readonly DateTime SyncInstant = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly TargetDate = new(2026, 9, 15);
    private static readonly Guid RelevantShowId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const int RelevantTmdbId = 900101;
    private const int IrrelevantTmdbId = 555555;

    [Fact]
    public async Task SyncAsync_AggregatesAllPagesAndDeduplicatesIds()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages =
            [
                new TmdbChangesPageResult([RelevantTmdbId, IrrelevantTmdbId], 1, 2),
                new TmdbChangesPageResult([RelevantTmdbId, 777777], 2, 2)
            ]
        };

        var refreshService = new RecordingRefreshService();
        var service = CreateService(changesProvider, refreshService);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(2, changesProvider.Calls.Count);
        Assert.Equal(3, result.ChangedTmdbIdsObserved);
        Assert.Equal(1, result.RelevantTargets);
        Assert.Equal(1, result.Refreshed);
        Assert.Equal(RelevantShowId, Assert.Single(refreshService.RefreshedTvShowIds));
    }

    [Fact]
    public async Task SyncAsync_IrrelevantChangedId_DoesNotRefresh()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbChangesPageResult([IrrelevantTmdbId], 1, 1)]
        };

        var refreshService = new RecordingRefreshService();
        var service = CreateService(changesProvider, refreshService);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(0, result.RelevantTargets);
        Assert.Equal(0, result.Refreshed);
        Assert.Empty(refreshService.RefreshedTvShowIds);
    }

    [Fact]
    public async Task SyncAsync_MultipleSignalsForSameShow_RefreshesOnce()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbChangesPageResult([RelevantTmdbId, RelevantTmdbId], 1, 1)]
        };

        var refreshService = new RecordingRefreshService();
        var service = CreateService(changesProvider, refreshService);

        await service.SyncAsync(SyncInstant);

        Assert.Single(refreshService.RefreshedTvShowIds);
    }

    [Fact]
    public async Task SyncAsync_InvokesPostRefreshBoundaryUsingWindowEndDate()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbChangesPageResult([RelevantTmdbId], 1, 1)]
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
            Pages = [new TmdbChangesPageResult([RelevantTmdbId], 1, 2)],
            FailOnPage = 2
        };

        var checkpointRepository = new FakeCheckpointRepository();
        var service = CreateService(changesProvider, new RecordingRefreshService(), checkpointRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncAsync(SyncInstant));

        Assert.Null(checkpointRepository.LastCompletedEndDate);
    }

    [Fact]
    public async Task SyncAsync_FailedShowRefresh_SkipsTitleAndAdvancesCheckpoint()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbChangesPageResult([RelevantTmdbId], 1, 1)]
        };

        var checkpointRepository = new FakeCheckpointRepository();
        var refreshService = new RecordingRefreshService
        {
            Outcome = TmdbChangesTargetRefreshOutcome.Failed
        };
        var service = CreateService(changesProvider, refreshService, checkpointRepository);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(TargetDate, checkpointRepository.LastCompletedEndDate);
        Assert.Equal(1, result.Failed);
        Assert.Equal(0, result.Refreshed);
    }

    [Fact]
    public async Task SyncAsync_UnavailableShowRefresh_SkipsTitleAndAdvancesCheckpoint()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbChangesPageResult([RelevantTmdbId], 1, 1)]
        };

        var checkpointRepository = new FakeCheckpointRepository();
        var refreshService = new RecordingRefreshService
        {
            Outcome = TmdbChangesTargetRefreshOutcome.SkippedUnavailable
        };
        var service = CreateService(changesProvider, refreshService, checkpointRepository);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(TargetDate, checkpointRepository.LastCompletedEndDate);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Refreshed);
    }

    [Fact]
    public async Task SyncAsync_ExceptionDuringRefresh_SkipsTitleAndAdvancesCheckpoint()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbChangesPageResult([RelevantTmdbId], 1, 1)]
        };

        var checkpointRepository = new FakeCheckpointRepository();
        var refreshService = new RecordingRefreshService { ShouldThrow = true };
        var service = CreateService(changesProvider, refreshService, checkpointRepository);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(TargetDate, checkpointRepository.LastCompletedEndDate);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task SyncAsync_SuccessfulWindow_AdvancesCheckpoint()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            Pages = [new TmdbChangesPageResult([], 1, 1)]
        };

        var checkpointRepository = new FakeCheckpointRepository();
        var service = CreateService(changesProvider, new RecordingRefreshService(), checkpointRepository);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(TargetDate, checkpointRepository.LastCompletedEndDate);
        Assert.Equal(TargetDate, result.LastCompletedEndDate);
    }

    [Fact]
    public async Task SyncAsync_LongCatchUp_ProcessesChunksSequentiallyAndAdvancesToTarget()
    {
        var changesProvider = new FakeTmdbTvChangesProvider
        {
            PagesByWindow =
            {
                [(new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 13))] =
                    [new TmdbChangesPageResult([], 1, 1)],
                [(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20))] =
                    [new TmdbChangesPageResult([], 1, 1)]
            }
        };

        var checkpointRepository = new FakeCheckpointRepository
        {
            LastCompletedEndDate = new DateOnly(2026, 8, 31)
        };

        var service = CreateService(
            changesProvider,
            new RecordingRefreshService(),
            checkpointRepository);

        var result = await service.SyncAsync(new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, result.WindowsProcessed);
        Assert.Equal(new DateOnly(2026, 9, 20), checkpointRepository.LastCompletedEndDate);
    }

    private static TmdbTvChangesSyncService CreateService(
        FakeTmdbTvChangesProvider changesProvider,
        RecordingRefreshService refreshService,
        FakeCheckpointRepository? checkpointRepository = null) =>
        new(
            changesProvider,
            checkpointRepository ?? new FakeCheckpointRepository(),
            new FakeRelevanceRepository(
                new Dictionary<int, Guid> { [RelevantTmdbId] = RelevantShowId }),
            refreshService,
            NullLogger<TmdbTvChangesSyncService>.Instance);

    private sealed class FakeTmdbTvChangesProvider : ITmdbTvChangesProvider
    {
        public List<TmdbChangesPageResult> Pages { get; init; } = [];

        public Dictionary<(DateOnly Start, DateOnly End), List<TmdbChangesPageResult>> PagesByWindow { get; init; } =
            new();

        public int? FailOnPage { get; init; }

        public List<(DateOnly Start, DateOnly End, int Page)> Calls { get; } = [];

        public Task<TmdbChangesPageResult> GetTvChangesPageAsync(
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

    private sealed class FakeRelevanceRepository(IReadOnlyDictionary<int, Guid> relevantShows)
        : ICatalogChangesRelevanceRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> GetRelevantMovieIdsByTmdbIdAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());

        public Task<IReadOnlyDictionary<int, Guid>> GetRelevantTvShowIdsByTmdbIdAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(relevantShows);
    }

    private sealed class RecordingRefreshService : ITvShowChangesTargetedRefreshService
    {
        public bool ShouldThrow { get; init; }

        public TmdbChangesTargetRefreshOutcome Outcome { get; init; } = TmdbChangesTargetRefreshOutcome.Refreshed;

        public List<Guid> RefreshedTvShowIds { get; } = [];

        public List<DateOnly> BoundaryDates { get; } = [];

        public List<DateOnly> ChangeSignalDates { get; } = [];

        public Task<TmdbChangesTargetRefreshResult> RefreshRelevantShowAsync(
            Guid tvShowId,
            DateOnly boundaryDate,
            DateOnly changeSignalDate,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated refresh failure.");
            }

            if (Outcome == TmdbChangesTargetRefreshOutcome.Refreshed)
            {
                RefreshedTvShowIds.Add(tvShowId);
                BoundaryDates.Add(boundaryDate);
                ChangeSignalDates.Add(changeSignalDate);
            }

            return Task.FromResult(
                Outcome == TmdbChangesTargetRefreshOutcome.Refreshed
                    ? TmdbChangesTargetRefreshResult.Refreshed([])
                    : new TmdbChangesTargetRefreshResult(Outcome, []));
        }
    }
}
