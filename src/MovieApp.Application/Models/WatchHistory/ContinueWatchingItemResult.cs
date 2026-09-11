namespace MovieApp.Application.Models.WatchHistory;

public sealed record ContinueWatchingItemResult(
    Guid TvShowId,
    string Title,
    string? OriginalTitle,
    string? PosterUrl,
    string? BackdropUrl,
    DateOnly? FirstAirDate,
    decimal VoteAverage,
    int VoteCount,
    DateTime LastWatchedAt);
