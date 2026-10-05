namespace MovieApp.Application.Configuration;

public sealed class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";

    public bool Enabled { get; set; }

    public bool TmdbChangesEnabled { get; set; } = true;

    /// <summary>
    /// Hangfire cron for <c>TmdbMovieChangesSyncJob</c> (staggered UTC minute to reduce concurrent heavy jobs).
    /// </summary>
    public string TmdbMovieChangesCron { get; set; } = "5 */6 * * *";

    /// <summary>
    /// Hangfire cron for <c>TmdbTvChangesSyncJob</c> (staggered UTC minute to reduce concurrent heavy jobs).
    /// </summary>
    public string TmdbTvChangesCron { get; set; } = "20 */6 * * *";

    public bool CatalogMetadataFreshnessSafetyNetEnabled { get; set; } = true;

    public bool HotReleaseEnabled { get; set; } = true;

    /// <summary>
    /// Hangfire cron for <c>HotReleaseCheckJob</c> (staggered UTC minute to reduce concurrent heavy jobs).
    /// </summary>
    public string HotReleaseCron { get; set; } = "10 * * * *";

    public bool MovieReleaseEnabled { get; set; } = true;

    /// <summary>
    /// Hangfire cron for <c>MovieReleaseCheckJob</c> (staggered UTC minute to reduce concurrent heavy jobs).
    /// </summary>
    public string MovieReleaseCron { get; set; } = "25 * * * *";

    public bool TvUpcomingEpisodeSyncEnabled { get; set; } = true;

    public bool NotificationFanoutEnabled { get; set; } = true;

    public bool PushDeliveryEnabled { get; set; } = true;

    public bool NotificationInboxCleanupEnabled { get; set; } = true;

    public int FanoutBatchSize { get; set; } = 100;

    public int PreparationBatchSize { get; set; } = 100;
}
