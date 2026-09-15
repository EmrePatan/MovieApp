namespace MovieApp.Application.Configuration;

public sealed class TvUpcomingEpisodeSyncOptions
{
    public const string SectionName = "TvUpcomingEpisodeSync";

    public bool Enabled { get; set; }

    public int BatchSize { get; set; } = 25;

    public int FreshnessTtlHours { get; set; } = 6;
}
