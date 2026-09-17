namespace MovieApp.Application.Configuration;

public sealed class InsightsOptions
{
    public const string SectionName = "Insights";

    public int SummaryCacheTtlMinutes { get; set; } = 5;

    public int AnalyticsCacheTtlMinutes { get; set; } = 5;
}
