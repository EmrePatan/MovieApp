namespace MovieApp.Application.Models.Catalog;

public enum CatalogGenreBackfillItemOutcome
{
    Succeeded,
    Unrepairable,
    Failed,
    Skipped,
    SkippedUnavailable
}
