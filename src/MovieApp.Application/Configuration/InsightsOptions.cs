namespace MovieApp.Application.Configuration;

public sealed class InsightsOptions
{
    public const string SectionName = "Insights";

    /// <summary>
    /// How long a built Insights v3 payload stays readable. The cache key includes the
    /// per-user generation, so a watch or library write drops it immediately. Twelve hours
    /// keeps an idle reader off the multi-second rebuild that a 5-minute TTL forced.
    /// </summary>
    public int AnalyticsCacheTtlMinutes { get; set; } = 12 * 60;
}
