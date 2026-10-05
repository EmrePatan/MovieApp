namespace MovieApp.Application.Configuration;

public sealed class CatalogMetadataFreshnessOptions
{
    public const string SectionName = "CatalogMetadataFreshness";

    public bool SafetyNetEnabled { get; set; } = true;

    /// <summary>
    /// Metadata older than this threshold (or never refreshed) is eligible for the safety-net job.
    /// </summary>
    public int FreshnessThresholdHours { get; set; } = 72;

    public int SafetyNetBatchSize { get; set; } = 25;

    public int SafetyNetMaxItemsPerRun { get; set; } = 50;

    /// <summary>
    /// UTC cron for the metadata freshness safety-net (low priority).
    /// </summary>
    public string SafetyNetRecurringCron { get; set; } = "0 */2 * * *";

    /// <summary>
    /// Future release/air dates within this horizon qualify as discovery-relevant without a vote floor.
    /// </summary>
    public int UpcomingReleaseHorizonDays { get; set; } = 180;

    /// <summary>
    /// Titles seen on the weekly TMDB trending snapshot within this window are discovery-relevant.
    /// </summary>
    public int ProviderDiscoverySeenWindowDays { get; set; } = 14;
}
