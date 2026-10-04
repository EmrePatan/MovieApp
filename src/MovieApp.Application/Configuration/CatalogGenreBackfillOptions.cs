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

    /// <summary>
    /// Minimum wait before retrying after TMDB detail succeeds but returns no usable genres.
    /// Defaults to 7 days (aligned with Search provider refresh maximum).
    /// </summary>
    public int ProviderEmptyGenresRetryHours { get; set; } = 168;

    /// <summary>
    /// Minimum wait before retrying after TMDB is unavailable or the title is not found.
    /// Defaults to 24 hours (aligned with ExternalRatings negative cache window).
    /// </summary>
    public int ProviderUnavailableRetryHours { get; set; } = 24;

    /// <summary>
    /// Minimum wait before retrying after a transient genre repair failure.
    /// </summary>
    public int TransientFailureRetryMinutes { get; set; } = 15;
}
