namespace MovieApp.Contracts.TvShows;

public sealed record SeasonSummaryResponse(
    Guid Id,
    int SeasonNumber,
    string? Name,
    DateOnly? AirDate,
    int? EpisodeCount,
    string? PosterPath);
