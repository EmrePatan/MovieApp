namespace MovieApp.Application.Models.Catalog;

public sealed record CatalogGenreBackfillCandidate(
    Guid CatalogId,
    string ContentType,
    int TmdbId);
