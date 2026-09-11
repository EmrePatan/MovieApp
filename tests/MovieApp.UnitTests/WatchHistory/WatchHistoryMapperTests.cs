using MovieApp.Application.Mapping;

namespace MovieApp.UnitTests.WatchHistory;

public sealed class WatchHistoryMapperTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(18, 62, 29.03)]
    [InlineData(7, 7, 100)]
    [InlineData(4, 7, 57.14)]
    public void CalculateProgressPercentageReturnsExpectedValue(
        int watched,
        int total,
        decimal expected)
    {
        var result = WatchHistoryMapper.CalculateProgressPercentage(watched, total);
        Assert.Equal(expected, result);
    }
}
