namespace MovieApp.Application.Models.Providers;

public sealed record MovieProviderSummary(
    string ExternalId,
    int? TmdbId,
    int? TvdbId,
    string? ImdbId,
    string Title,
    string? Overview,
    DateOnly? ReleaseDate,
    string? PosterPath,
    decimal VoteAverage,
    int VoteCount);
