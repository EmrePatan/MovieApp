using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.ProductMetrics;
using MovieApp.Contracts.ProductMetrics;

namespace MovieApp.IntegrationTests.ProductMetrics;

[CollectionDefinition("ProductMetricsApi")]
public sealed class ProductMetricsApiTestsDefinition : ICollectionFixture<ProductMetricsFixture>;

[Collection("ProductMetricsApi")]
public sealed class ProductMetricsApiTests(ProductMetricsFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task IncrementAcceptsAllowListedMetricWithoutAuthentication()
    {
        await ProductMetricsFixture.ResetAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/product-metrics/increment",
            new IncrementProductMetricRequest(ProductMetricNames.AdvancedDiscoverOpened));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var context = ProductMetricsFixture.CreateContext();
        var metric = await context.ProductMetricDaily.SingleAsync();
        Assert.Equal(ProductMetricNames.AdvancedDiscoverOpened, metric.MetricName);
        Assert.Equal(1, metric.Count);
    }

    [Fact]
    public async Task IncrementRejectsUnknownMetricName()
    {
        await ProductMetricsFixture.ResetAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/product-metrics/increment",
            new IncrementProductMetricRequest("not_allowed"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await using var context = ProductMetricsFixture.CreateContext();
        Assert.Equal(0, await context.ProductMetricDaily.CountAsync());
    }
}
