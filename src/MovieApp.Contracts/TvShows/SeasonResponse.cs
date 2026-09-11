namespace MovieApp.Contracts.TvShows;

public sealed record SeasonResponse(
    Guid Id,
    Guid TvShowId,
    int SeasonNumber,
    string? Name,
    string? Overview,
    DateOnly? AirDate,
    int? EpisodeCount,
    string? PosterPath,
    IReadOnlyList<EpisodeSummaryResponse> Episodes);
