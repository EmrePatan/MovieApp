using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsRatingsAnalyticsBuilderTests
{
    [Fact]
    public void BuildReturnsEmptyForZeroRatings()
    {
        var raw = CreateRaw(0, []);

        var result = InsightsRatingsAnalyticsBuilder.Build(raw);

        Assert.Equal(0, result.RatingCount);
        Assert.Null(result.AverageStarRating);
        Assert.Empty(result.Distribution);
        Assert.Null(result.MostUsedStars);
    }

    [Fact]
    public void BuildHidesMostUsedStarsBelowFiveRatings()
    {
        var raw = CreateRaw(4, [(8, 2), (10, 2)]);

        var result = InsightsRatingsAnalyticsBuilder.Build(raw);

        Assert.Equal(4.5m, result.AverageStarRating);
        Assert.Null(result.MostUsedStars);
    }

    [Fact]
    public void BuildExposesMostUsedStarsAtFiveRatingsWithDeterministicTieBreak()
    {
        var raw = CreateRaw(5, [(8, 2), (10, 3)]);

        var result = InsightsRatingsAnalyticsBuilder.Build(raw);

        Assert.Equal(5, result.MostUsedStars);
    }

    private static InsightsAnalyticsRawData CreateRaw(
        int ratingsCount,
        IReadOnlyList<(int Score, int Count)> scores) =>
        new(
            DateTime.UtcNow,
            0,
            0,
            0,
            ratingsCount,
            [],
            [],
            [],
            0,
            0,
            0,
            0,
            scores,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
}
