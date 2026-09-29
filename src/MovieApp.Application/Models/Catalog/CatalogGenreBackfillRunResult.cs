namespace MovieApp.Application.Models.Catalog;

public sealed record CatalogGenreBackfillRunResult(
    int BatchesProcessed,
    int Selected,
    int Succeeded,
    int Unrepairable,
    int Failed,
    int Skipped,
    int MoviesProcessed,
    int TvShowsProcessed,
    int RemainingAfterRun);
