using Microsoft.Extensions.Logging.Abstractions;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Abstractions.Providers;
using MovieApp.Application.Models.Changes;
using MovieApp.Application.Services.MovieChanges;

namespace MovieApp.UnitTests.MovieChanges;

public sealed class TmdbMovieChangesSyncServiceTests
{
    private static readonly DateTime SyncInstant = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly TargetDate = new(2026, 9, 15);
    private static readonly Guid RelevantMovieId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const int RelevantTmdbId = 100200;
    private const int IrrelevantTmdbId = 999999;

    [Fact]
    public async Task SyncAsync_AggregatesPagesAndRefreshesRelevantMoviesOnly()
    {
        var changesProvider = new FakeMovieChangesProvider
        {
            Pages =
            [
                new TmdbChangesPageResult([RelevantTmdbId, IrrelevantTmdbId], 1, 1)
            ]
        };

        var refreshService = new RecordingMovieRefreshService();
        var service = CreateService(changesProvider, refreshService);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(2, result.ChangedTmdbIdsObserved);
        Assert.Equal(1, result.RelevantTargets);
        Assert.Equal(1, result.Refreshed);
        Assert.Equal(RelevantMovieId, Assert.Single(refreshService.RefreshedMovieIds));
    }

    [Fact]
    public async Task SyncAsync_UnavailableMovie_IsSkippedAndCheckpointAdvances()
    {
        var changesProvider = new FakeMovieChangesProvider
        {
            Pages = [new TmdbChangesPageResult([RelevantTmdbId], 1, 1)]
        };

        var refreshService = new RecordingMovieRefreshService
        {
            Outcome = TmdbChangesTargetRefreshOutcome.SkippedUnavailable
        };

        var checkpointRepository = new FakeCheckpointRepository();
        var service = CreateService(changesProvider, refreshService, checkpointRepository);

        var result = await service.SyncAsync(SyncInstant);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(TargetDate, checkpointRepository.LastCompletedEndDate);
    }

    private static TmdbMovieChangesSyncService CreateService(
        FakeMovieChangesProvider changesProvider,
        RecordingMovieRefreshService refreshService,
        FakeCheckpointRepository? checkpointRepository = null) =>
        new(
            changesProvider,
            checkpointRepository ?? new FakeCheckpointRepository(),
            new FakeRelevanceRepository(
                new Dictionary<int, Guid> { [RelevantTmdbId] = RelevantMovieId }),
            refreshService,
            NullLogger<TmdbMovieChangesSyncService>.Instance);

    private sealed class FakeMovieChangesProvider : ITmdbMovieChangesProvider
    {
        public List<TmdbChangesPageResult> Pages { get; init; } = [];

        public Task<TmdbChangesPageResult> GetMovieChangesPageAsync(
            DateOnly startDate,
            DateOnly endDate,
            int page,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Pages[page - 1]);
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

    private sealed class FakeRelevanceRepository(IReadOnlyDictionary<int, Guid> relevantMovies)
        : ICatalogChangesRelevanceRepository
    {
        public Task<IReadOnlyDictionary<int, Guid>> GetRelevantMovieIdsByTmdbIdAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(relevantMovies);

        public Task<IReadOnlyDictionary<int, Guid>> GetRelevantTvShowIdsByTmdbIdAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Guid>>(new Dictionary<int, Guid>());
    }

    private sealed class RecordingMovieRefreshService : IMovieChangesTargetedRefreshService
    {
        public TmdbChangesTargetRefreshOutcome Outcome { get; init; } = TmdbChangesTargetRefreshOutcome.Refreshed;

        public List<Guid> RefreshedMovieIds { get; } = [];

        public Task<TmdbChangesTargetRefreshResult> RefreshRelevantMovieAsync(
            Guid movieId,
            CancellationToken cancellationToken = default)
        {
            if (Outcome == TmdbChangesTargetRefreshOutcome.Refreshed)
            {
                RefreshedMovieIds.Add(movieId);
            }

            return Task.FromResult(
                Outcome == TmdbChangesTargetRefreshOutcome.Refreshed
                    ? TmdbChangesTargetRefreshResult.Refreshed([])
                    : new TmdbChangesTargetRefreshResult(Outcome, []));
        }
    }
}
