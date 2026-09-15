using MovieApp.Application.ProductMetrics;

namespace MovieApp.Application.Validation;

public static class ProductMetricValidator
{
    public static SearchQueryValidationResult Validate(string? metricName)
    {
        if (ProductMetricNames.IsAllowed(metricName))
        {
            return SearchQueryValidationResult.Success();
        }

        return SearchQueryValidationResult.Failure("Metric name is not allowed.");
    }

    public static string Normalize(string metricName) => metricName.Trim();
}
