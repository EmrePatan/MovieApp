namespace MovieApp.Application.Models.TvShows;

public sealed record EpisodeResult(
    Guid Id,
    Guid TvShowId,
    Guid SeasonId,
    int SeasonNumber,
    int EpisodeNumber,
    string? Name,
    string? Overview,
    DateOnly? AirDate,
    int? RuntimeMinutes,
    string? StillPath,
    decimal VoteAverage,
    int VoteCount);
