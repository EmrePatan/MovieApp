namespace MovieApp.Application.Configuration;

public sealed class InsightsOptions
{
    public const string SectionName = "Insights";

    public int AnalyticsCacheTtlMinutes { get; set; } = 5;
}
