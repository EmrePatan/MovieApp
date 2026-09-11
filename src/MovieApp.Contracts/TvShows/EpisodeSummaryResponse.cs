namespace MovieApp.Contracts.TvShows;

public sealed record EpisodeSummaryResponse(
    Guid Id,
    int EpisodeNumber,
    string? Name,
    DateOnly? AirDate,
    int? RuntimeMinutes,
    string? StillPath,
    decimal VoteAverage,
    int VoteCount);
