namespace MovieApp.Application.Models.Keywords;

public sealed record CatalogKeywordBackfillBatchResult(
    int Selected,
    int Succeeded,
    int Failed,
    int Skipped,
    int MoviesProcessed,
    int TvShowsProcessed);
