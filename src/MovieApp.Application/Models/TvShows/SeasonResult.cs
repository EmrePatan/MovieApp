namespace MovieApp.Application.Models.TvShows;

public sealed record SeasonResult(
    Guid Id,
    Guid TvShowId,
    int SeasonNumber,
    string? Name,
    string? Overview,
    DateOnly? AirDate,
    int? EpisodeCount,
    string? PosterPath,
    IReadOnlyList<EpisodeSummaryResult> Episodes);
