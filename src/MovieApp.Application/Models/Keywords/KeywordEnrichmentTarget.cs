namespace MovieApp.Application.Models.Keywords;

public sealed record KeywordEnrichmentTarget(int TmdbId, DateTime? KeywordsSyncedAtUtc);
