namespace MovieApp.Application.Models.TvShows;

public sealed record SeasonSummaryResult(
    Guid Id,
    int SeasonNumber,
    string? Name,
    DateOnly? AirDate,
    int? EpisodeCount,
    string? PosterPath);
