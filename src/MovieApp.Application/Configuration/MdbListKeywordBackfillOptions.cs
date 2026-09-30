namespace MovieApp.Application.Configuration;

public sealed class MdbListKeywordBackfillOptions
{
    public const string SectionName = "MdbListKeywordBackfill";

    public bool Enabled { get; set; }

    public int BatchSize { get; set; } = 100;

    public int MaxConcurrency { get; set; } = 2;

    public int DelayBetweenItemsMs { get; set; } = 250;

    public string RecurringCron { get; set; } = "0 * * * *";
}
