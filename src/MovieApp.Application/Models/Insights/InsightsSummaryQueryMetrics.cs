namespace MovieApp.Application.Models.Insights;

public sealed class InsightsSummaryQueryMetrics
{
    public int DbRoundTrips { get; set; }

    public long DbTotalMs { get; set; }
}
