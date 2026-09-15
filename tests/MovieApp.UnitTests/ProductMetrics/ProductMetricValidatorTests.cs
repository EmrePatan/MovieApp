using MovieApp.Application.ProductMetrics;
using MovieApp.Application.Validation;

namespace MovieApp.UnitTests.ProductMetrics;

public sealed class ProductMetricValidatorTests
{
    [Theory]
    [InlineData(ProductMetricNames.DiscoverOpened)]
    [InlineData(ProductMetricNames.StreamingServicesOpened)]
    [InlineData(ProductMetricNames.ContentDetailOpened)]
    [InlineData(ProductMetricNames.PickSomethingUsed)]
    public void ValidateAcceptsAllowListedMetrics(string metricName)
    {
        var result = ProductMetricValidator.Validate(metricName);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown_metric")]
    [InlineData("screen_view")]
    public void ValidateRejectsUnknownMetrics(string? metricName)
    {
        var result = ProductMetricValidator.Validate(metricName);

        Assert.False(result.IsValid);
    }
}
