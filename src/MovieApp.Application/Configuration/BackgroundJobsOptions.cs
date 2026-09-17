namespace MovieApp.Application.Configuration;

public sealed class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";

    public bool Enabled { get; set; }

    public bool TmdbChangesEnabled { get; set; } = true;

    public bool HotReleaseEnabled { get; set; } = true;

    public bool MovieReleaseEnabled { get; set; } = true;

    public bool TvUpcomingEpisodeSyncEnabled { get; set; } = true;

    public bool NotificationFanoutEnabled { get; set; } = true;

    public bool PushDeliveryEnabled { get; set; } = true;

    public bool NotificationInboxCleanupEnabled { get; set; } = true;

    public int FanoutBatchSize { get; set; } = 100;

    public int PreparationBatchSize { get; set; } = 100;
}
