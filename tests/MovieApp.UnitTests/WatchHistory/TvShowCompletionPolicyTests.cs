using MovieApp.Application.Services.WatchHistory;
using MovieApp.Domain.Enums;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class TvShowCompletionPolicyTests
{
    [Theory]
    [InlineData(TvShowStatus.Ended, true)]
    [InlineData(TvShowStatus.Canceled, true)]
    [InlineData(TvShowStatus.ReturningSeries, false)]
    [InlineData(TvShowStatus.InProduction, false)]
    [InlineData(TvShowStatus.Planned, false)]
    [InlineData(TvShowStatus.Pilot, false)]
    public void IsConcludedOnlyForShowsThatCannotReceiveNewEpisodes(TvShowStatus status, bool expected) =>
        Assert.Equal(expected, TvShowCompletionPolicy.IsConcluded(status));

    [Fact]
    public void CaughtUpReturningSeriesIsNotCompleted() =>
        Assert.False(TvShowCompletionPolicy.IsCompleted(isConcluded: false, 20, 20));

    [Fact]
    public void FullyWatchedEndedSeriesIsCompleted() =>
        Assert.True(TvShowCompletionPolicy.IsCompleted(isConcluded: true, 20, 20));

    [Fact]
    public void EndedSeriesWithUnwatchedEpisodesIsNotCompleted() =>
        Assert.False(TvShowCompletionPolicy.IsCompleted(isConcluded: true, 20, 19));

    [Fact]
    public void ShowWithoutKnownEpisodesIsNotCompleted() =>
        Assert.False(TvShowCompletionPolicy.IsCompleted(isConcluded: true, 0, 0));

    [Theory]
    [InlineData(8, 10, 8)]
    [InlineData(0, 10, 10)]
    [InlineData(0, null, 0)]
    [InlineData(0, -3, 0)]
    public void SeasonTotalFallsBackToSummaryCountOnlyWhenNothingIsIngested(
        int ingested,
        int? summary,
        int expected) =>
        Assert.Equal(expected, TvShowCompletionPolicy.ResolveSeasonEpisodeTotal(ingested, summary));
}
