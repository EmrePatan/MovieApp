using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.HotRelease;
using MovieApp.Application.Services.HotRelease;

namespace MovieApp.UnitTests.HotRelease;

public sealed class HotReleaseCheckServiceTests
{
    private static readonly DateOnly Boundary = new(2026, 9, 15);
    private static readonly Guid ShowA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ShowB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task RunAsync_MultipleFollowersSameShow_ProcessesOneCandidate()
    {
        var processor = new RecordingProcessor();
        var service = new HotReleaseCheckService(
            new FakeCandidateRepository([new HotReleaseCandidate(ShowA, 1, null, null)]),
            processor);

        var result = await service.RunAsync(Boundary);

        Assert.Equal(1, result.Candidates);
        Assert.Equal(1, result.Checked);
        Assert.Equal(ShowA, Assert.Single(processor.ProcessedTvShowIds));
    }

    [Fact]
    public async Task RunAsync_FailedCandidate_ContinuesOtherShowsAndReportsFailure()
    {
        var processor = new RecordingProcessor { FailTvShowId = ShowA };
        var service = new HotReleaseCheckService(
            new FakeCandidateRepository(
            [
                new HotReleaseCandidate(ShowA, 1, null, null),
                new HotReleaseCandidate(ShowB, 2, null, null)
            ]),
            processor);

        var result = await service.RunAsync(Boundary);

        Assert.Equal(2, result.Candidates);
        Assert.Equal(1, result.Checked);
        Assert.Equal(1, result.Failures);
        Assert.Equal(ShowB, Assert.Single(processor.ProcessedTvShowIds));
    }

    [Fact]
    public async Task RunAsync_RepeatedBoundaryCheck_RemainsIdempotent()
    {
        var processor = new RecordingProcessor { EventsCreated = 2 };
        var service = new HotReleaseCheckService(
            new FakeCandidateRepository([new HotReleaseCandidate(ShowA, 1, null, null)]),
            processor);

        var first = await service.RunAsync(Boundary);
        processor.EventsCreated = 0;
        var second = await service.RunAsync(Boundary);

        Assert.Equal(2, first.ReleaseEventsCreated);
        Assert.Equal(0, second.ReleaseEventsCreated);
        Assert.Equal(2, processor.ProcessCount);
    }

    private sealed class FakeCandidateRepository(IReadOnlyList<HotReleaseCandidate> candidates)
        : IHotReleaseCandidateRepository
    {
        public Task<IReadOnlyList<HotReleaseCandidate>> GetCandidatesAsync(
            DateOnly boundaryDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(candidates);
    }

    private sealed class RecordingProcessor : IHotReleaseCandidateProcessor
    {
        public Guid? FailTvShowId { get; set; }

        public int EventsCreated { get; set; }

        public List<Guid> ProcessedTvShowIds { get; } = [];

        public int ProcessCount { get; private set; }

        public Task<HotReleaseCandidateProcessResult> ProcessAsync(
            HotReleaseCandidate candidate,
            DateOnly boundaryDate,
            CancellationToken cancellationToken = default)
        {
            ProcessCount++;
            if (candidate.TvShowId == FailTvShowId)
            {
                throw new InvalidOperationException("Simulated hot-check failure.");
            }

            ProcessedTvShowIds.Add(candidate.TvShowId);
            return Task.FromResult(new HotReleaseCandidateProcessResult(false, EventsCreated));
        }
    }
}
