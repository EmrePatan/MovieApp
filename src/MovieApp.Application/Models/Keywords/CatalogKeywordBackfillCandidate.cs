namespace MovieApp.Application.Models.Keywords;

public sealed record CatalogKeywordBackfillCandidate(
    Guid CatalogId,
    string ContentType,
    int TmdbId);
