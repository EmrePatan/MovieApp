using MovieApp.Application.Services.TvShowChanges;

namespace MovieApp.UnitTests.TvShowChanges;

public sealed class TmdbTvChangesWindowPlannerTests
{
    [Fact]
    public void BuildChunks_SplitsRangesLongerThanFourteenDays()
    {
        var start = new DateOnly(2026, 9, 1);
        var end = new DateOnly(2026, 9, 20);

        var chunks = TmdbTvChangesWindowPlanner.BuildChunks(start, end);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(new DateOnly(2026, 9, 1), chunks[0].Start);
        Assert.Equal(new DateOnly(2026, 9, 14), chunks[0].End);
        Assert.Equal(new DateOnly(2026, 9, 15), chunks[1].Start);
        Assert.Equal(new DateOnly(2026, 9, 20), chunks[1].End);
    }

    [Fact]
    public void DetermineNextWindowStart_UsesTwoDayInitialWindowWhenNoCheckpoint()
    {
        var targetDate = new DateOnly(2026, 9, 15);

        var start = TmdbTvChangesWindowPlanner.DetermineNextWindowStart(null, targetDate);

        Assert.Equal(new DateOnly(2026, 9, 14), start);
    }

    [Fact]
    public void DetermineNextWindowStart_OverlapsPreviousCompletedEndDate()
    {
        var lastCompleted = new DateOnly(2026, 9, 10);
        var targetDate = new DateOnly(2026, 9, 15);

        var start = TmdbTvChangesWindowPlanner.DetermineNextWindowStart(lastCompleted, targetDate);

        Assert.Equal(lastCompleted, start);
    }
}
