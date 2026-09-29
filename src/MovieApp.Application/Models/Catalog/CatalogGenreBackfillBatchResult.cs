namespace MovieApp.Application.Models.Catalog;

public sealed record CatalogGenreBackfillBatchResult(
    int Selected,
    int Succeeded,
    int Unrepairable,
    int Failed,
    int Skipped,
    int MoviesProcessed,
    int TvShowsProcessed);
