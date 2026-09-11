namespace MovieApp.Application.Models.WatchHistory;

public sealed record RecentWatchHistoryItemResult(
    string Type,
    Guid? MovieId,
    Guid? EpisodeId,
    Guid? TvShowId,
    string? Title,
    string? TvShowTitle,
    int? SeasonNumber,
    int? EpisodeNumber,
    string? EpisodeTitle,
    DateTime WatchedAt);
