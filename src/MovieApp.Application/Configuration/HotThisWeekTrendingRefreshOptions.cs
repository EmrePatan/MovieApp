namespace MovieApp.Application.Configuration;

public sealed class HotThisWeekTrendingRefreshOptions
{
    public const string SectionName = "HotThisWeekTrendingRefresh";

    public bool Enabled { get; set; } = true;

    public int SnapshotTtlDays { get; set; } = 7;

    /// <summary>
    /// Hangfire cron for refreshing the TMDB weekly trending page-1 snapshot (UTC).
    /// </summary>
    public string RefreshCron { get; set; } = "0 * * * *";
}
