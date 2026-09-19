namespace MovieApp.Application.Configuration;

public sealed class HotThisWeekTrendingRefreshOptions
{
    public const string SectionName = "HotThisWeekTrendingRefresh";

    public bool Enabled { get; set; } = true;

    public int SnapshotTtlDays { get; set; } = 7;
}
