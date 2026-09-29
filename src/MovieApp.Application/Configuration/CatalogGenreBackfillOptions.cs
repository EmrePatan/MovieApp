namespace MovieApp.Application.Configuration;

public sealed class CatalogGenreBackfillOptions
{
    public const string SectionName = "CatalogGenreBackfill";

    public bool Enabled { get; set; }

    public int BatchSize { get; set; } = 50;

    public int MaxConcurrency { get; set; } = 1;

    public int MaxBatchesPerRun { get; set; } = 1;

    public int DelayBetweenItemsMs { get; set; } = 250;

    public int DelayBetweenBatchesMs { get; set; } = 5_000;

    public string RecurringCron { get; set; } = "0 * * * *";
}
