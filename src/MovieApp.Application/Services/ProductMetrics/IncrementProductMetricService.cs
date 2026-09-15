using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Exceptions;
using MovieApp.Application.Validation;

namespace MovieApp.Application.Services.ProductMetrics;

public sealed class IncrementProductMetricService(
    IProductMetricDailyRepository productMetricDailyRepository) : IIncrementProductMetricService
{
    public async Task IncrementAsync(string metricName, CancellationToken cancellationToken = default)
    {
        var validation = ProductMetricValidator.Validate(metricName);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.ErrorMessage ?? "Metric name is not allowed.");
        }

        var utcNow = DateTime.UtcNow;
        var metricDate = DateOnly.FromDateTime(utcNow);

        await productMetricDailyRepository.IncrementAsync(
            ProductMetricValidator.Normalize(metricName),
            metricDate,
            utcNow,
            cancellationToken);
    }
}
