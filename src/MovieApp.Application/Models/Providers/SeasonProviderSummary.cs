namespace MovieApp.Application.Models.Providers;

public sealed record SeasonProviderSummary(
    int SeasonNumber,
    string? Name,
    DateOnly? AirDate,
    int? EpisodeCount,
    string? PosterPath);
