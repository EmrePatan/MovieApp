using MovieApp.Domain.Entities;

namespace MovieApp.UnitTests.ProductMetrics;

public sealed class ProductMetricDailyPrivacyTests
{
    [Fact]
    public void ProductMetricDailyStoresOnlyAggregateFields()
    {
        var propertyNames = typeof(ProductMetricDaily)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(["Count", "Date", "MetricName", "UpdatedAtUtc"], propertyNames);
    }
}
