namespace MovieApp.Application.Models.Insights;

public sealed class InsightsAnalyticsQueryMetrics
{
    public int DbRoundTrips { get; set; }

    public long DbTotalMs { get; set; }

    public long ActivityMs { get; set; }

    public long TasteErasMs { get; set; }

    public long RuntimeMs { get; set; }

    public long RatingsMs { get; set; }

    public long MilestonesMs { get; set; }
}
