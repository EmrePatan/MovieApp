namespace MovieApp.Application.Models.Catalog;

public sealed record CatalogGenreBackfillCoverageSnapshot(
    int MovieGenreLess,
    int TvGenreLess,
    int MovieEligible,
    int TvEligible)
{
    public int OverallRemaining => MovieGenreLess + TvGenreLess;
}
