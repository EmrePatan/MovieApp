namespace MovieApp.Contracts.TvShows;

public sealed record EpisodeResponse(
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
