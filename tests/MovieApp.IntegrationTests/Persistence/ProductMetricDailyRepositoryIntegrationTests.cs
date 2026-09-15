using Microsoft.EntityFrameworkCore;
using MovieApp.Application.ProductMetrics;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;

namespace MovieApp.IntegrationTests.Persistence;

[Collection("CatalogPersistence")]
public sealed class ProductMetricDailyRepositoryIntegrationTests
{
    [Fact]
    public async Task IncrementAsyncIncrementsSameDayRowAtomically()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearMetricsAsync(context);

        var metricDate = new DateOnly(2026, 9, 15);
        var updatedAtUtc = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        using var barrier = new Barrier(5);

        await Task.WhenAll(
            RunConcurrentIncrementAsync(metricDate, updatedAtUtc, barrier),
            RunConcurrentIncrementAsync(metricDate, updatedAtUtc, barrier),
            RunConcurrentIncrementAsync(metricDate, updatedAtUtc, barrier),
            RunConcurrentIncrementAsync(metricDate, updatedAtUtc, barrier),
            RunConcurrentIncrementAsync(metricDate, updatedAtUtc, barrier));

        var metric = await context.ProductMetricDaily.SingleAsync();
        Assert.Equal(metricDate, metric.Date);
        Assert.Equal(ProductMetricNames.DiscoverOpened, metric.MetricName);
        Assert.Equal(5, metric.Count);
    }

    [Fact]
    public async Task IncrementAsyncCreatesSeparateRowsPerDay()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearMetricsAsync(context);

        var repository = new ProductMetricDailyRepository(context);
        var firstDay = new DateOnly(2026, 9, 15);
        var secondDay = new DateOnly(2026, 9, 16);

        await repository.IncrementAsync(
            ProductMetricNames.StreamingServicesOpened,
            firstDay,
            new DateTime(2026, 9, 15, 10, 0, 0, DateTimeKind.Utc));
        await repository.IncrementAsync(
            ProductMetricNames.StreamingServicesOpened,
            secondDay,
            new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc));

        var metrics = await context.ProductMetricDaily
            .Where(metric => metric.MetricName == ProductMetricNames.StreamingServicesOpened)
            .OrderBy(metric => metric.Date)
            .ToListAsync();

        Assert.Equal(2, metrics.Count);
        Assert.All(metrics, metric => Assert.Equal(1, metric.Count));
    }

    [Fact]
    public async Task IncrementAsyncStoresOnlyAggregateFields()
    {
        await using var context = CatalogPersistenceFixture.CreateContext();
        await ClearMetricsAsync(context);

        var repository = new ProductMetricDailyRepository(context);
        var metricDate = new DateOnly(2026, 9, 15);
        var updatedAtUtc = new DateTime(2026, 9, 15, 8, 30, 0, DateTimeKind.Utc);

        await repository.IncrementAsync(
            ProductMetricNames.ContentDetailOpened,
            metricDate,
            updatedAtUtc);

        var metric = await context.ProductMetricDaily.SingleAsync();
        Assert.Equal(metricDate, metric.Date);
        Assert.Equal(ProductMetricNames.ContentDetailOpened, metric.MetricName);
        Assert.Equal(1, metric.Count);
        Assert.Equal(updatedAtUtc, metric.UpdatedAtUtc);
    }

    private static Task RunConcurrentIncrementAsync(
        DateOnly metricDate,
        DateTime updatedAtUtc,
        Barrier barrier) =>
        Task.Run(async () =>
        {
            await using var context = CatalogPersistenceFixture.CreateContext();
            var repository = new ProductMetricDailyRepository(context);
            barrier.SignalAndWait();
            await repository.IncrementAsync(
                ProductMetricNames.DiscoverOpened,
                metricDate,
                updatedAtUtc);
        });

    private static async Task ClearMetricsAsync(ApplicationDbContext context)
    {
        await context.ProductMetricDaily.ExecuteDeleteAsync();
    }
}
