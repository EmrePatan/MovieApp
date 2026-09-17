using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.Insights;

namespace MovieApp.UnitTests.Insights;

public sealed class InsightsErasBuilderTests
{
    [Fact]
    public void MapYearToBucketUsesExpectedBoundaries()
    {
        Assert.Equal("2020s", InsightsErasBuilder.MapYearToBucket(2020));
        Assert.Equal("2010s", InsightsErasBuilder.MapYearToBucket(2019));
        Assert.Equal("1990s", InsightsErasBuilder.MapYearToBucket(1999));
        Assert.Equal("Older", InsightsErasBuilder.MapYearToBucket(1989));
    }

    [Fact]
    public void BuildExcludesUnknownFromPercentageDenominator()
    {
        var raw = CreateRaw(
        [
            new InsightsDnaTitleData(2021, []),
            new InsightsDnaTitleData(2022, []),
            new InsightsDnaTitleData(null, []),
        ]);

        var result = InsightsErasBuilder.Build(raw);
        var twenties = result.Buckets.Single(bucket => bucket.Bucket == "2020s");

        Assert.Equal(2, twenties.Count);
        Assert.Equal(100.0m, twenties.Percent);
        Assert.Equal(1, result.UnknownCount);
    }

    private static InsightsAnalyticsRawData CreateRaw(IReadOnlyList<InsightsDnaTitleData> titles) =>
        new(
            DateTime.UtcNow,
            titles.Count,
            0,
            0,
            0,
            [],
            titles,
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
}
