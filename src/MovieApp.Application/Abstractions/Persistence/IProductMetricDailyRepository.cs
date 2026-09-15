namespace MovieApp.Application.Abstractions.Persistence;

public interface IProductMetricDailyRepository
{
    Task IncrementAsync(
        string metricName,
        DateOnly metricDate,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);
}
