using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsEstimatedTimeBuilderTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void BuildSumsKnownMovieAndEpisodeRuntimeWithCoverage()
    {
        var raw = new InsightsAnalyticsRawData(
            DateTime.UtcNow,
            3,
            2,
            0,
            0,
            [],
            [],
            [],
            120,
            2,
            45,
            1,
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var result = InsightsEstimatedTimeBuilder.Build(raw, Utc, DateTime.UtcNow);

        Assert.Equal(165, result.TotalEstimatedMinutes);
        Assert.Equal(120, result.MovieEstimatedMinutes);
        Assert.Equal(45, result.EpisodeEstimatedMinutes);
        Assert.Equal(3, result.KnownRuntimeItemCount);
        Assert.Equal(5, result.TotalWatchedItemCount);
        Assert.Equal(60.0m, result.CoveragePercent);
    }

    [Fact]
    public void BuildReturnsZeroCoverageWhenNoKnownRuntimeExists()
    {
        var raw = new InsightsAnalyticsRawData(
            DateTime.UtcNow,
            2,
            1,
            0,
            0,
            [],
            [],
            [],
            0,
            0,
            0,
            0,
            [],
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        var result = InsightsEstimatedTimeBuilder.Build(raw, Utc, DateTime.UtcNow);

        Assert.Equal(0, result.TotalEstimatedMinutes);
        Assert.Equal(0m, result.CoveragePercent);
    }
}
