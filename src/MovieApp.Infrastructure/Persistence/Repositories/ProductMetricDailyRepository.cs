using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class ProductMetricDailyRepository(ApplicationDbContext dbContext) : IProductMetricDailyRepository
{
    public async Task IncrementAsync(
        string metricName,
        DateOnly metricDate,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO product_metric_daily ("Date", "MetricName", "Count", "UpdatedAtUtc")
             VALUES ({metricDate}, {metricName}, 1, {updatedAtUtc})
             ON CONFLICT ("Date", "MetricName")
             DO UPDATE SET
                 "Count" = product_metric_daily."Count" + 1,
                 "UpdatedAtUtc" = {updatedAtUtc}
             """,
            cancellationToken);
    }
}
