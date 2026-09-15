namespace MovieApp.Application.Services.ProductMetrics;

public interface IIncrementProductMetricService
{
    Task IncrementAsync(string metricName, CancellationToken cancellationToken = default);
}
