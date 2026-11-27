namespace MovieApp.Application.Configuration;

public sealed class TvUpcomingEpisodeSyncOptions
{
    public const string SectionName = "TvUpcomingEpisodeSync";

    public bool Enabled { get; set; }

    public int BatchSize { get; set; } = 25;

    public int FreshnessTtlHours { get; set; } = 6;

    /// <summary>How long a failed show stays out of the batch so others get their turn.</summary>
    public int FailureCooldownHours { get; set; } = 6;
}
