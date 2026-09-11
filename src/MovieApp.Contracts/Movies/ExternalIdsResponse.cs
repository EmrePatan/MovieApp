namespace MovieApp.Contracts.Movies;

public sealed record ExternalIdsResponse(
    int? TmdbId,
    int? TvdbId,
    string? ImdbId);
