namespace MovieApp.Application.Models.TvShows;

public sealed record EpisodeSummaryResult(
    Guid Id,
    int EpisodeNumber,
    string? Name,
    DateOnly? AirDate,
    int? RuntimeMinutes,
    string? StillPath,
    decimal VoteAverage,
    int VoteCount);
