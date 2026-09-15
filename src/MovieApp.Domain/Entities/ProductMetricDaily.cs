namespace MovieApp.Domain.Entities;

public sealed class ProductMetricDaily
{
    public DateOnly Date { get; set; }

    public string MetricName { get; set; } = string.Empty;

    public long Count { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
