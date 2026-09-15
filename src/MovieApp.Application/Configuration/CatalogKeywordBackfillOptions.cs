namespace MovieApp.Application.Configuration;

public sealed class CatalogKeywordBackfillOptions
{
    public const string SectionName = "CatalogKeywordBackfill";

    public bool Enabled { get; set; }

    public int BatchSize { get; set; } = 25;

    public int MaxConcurrency { get; set; } = 2;

    public string RecurringCron { get; set; } = "0 * * * *";
}
