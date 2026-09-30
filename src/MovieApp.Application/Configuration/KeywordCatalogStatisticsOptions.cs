namespace MovieApp.Application.Configuration;

public sealed class KeywordCatalogStatisticsOptions
{
    public const string SectionName = "KeywordCatalogStatistics";

    public bool Enabled { get; set; } = true;

    public int MinimumVoteCount { get; set; } = 20;

    public double GenericDocumentFrequencyRatio { get; set; } = 0.10;

    public double GenericDampeningFactor { get; set; } = 0.5;

    /// <summary>
    /// Hangfire cron for refreshing catalog keyword DF/IDF (UTC). Default: daily at 03:00.
    /// </summary>
    public string RefreshCron { get; set; } = "0 3 * * *";

    public bool RefreshOnStartup { get; set; } = true;
}
