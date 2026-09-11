namespace MovieApp.Contracts.WatchHistory;

public sealed record RecentWatchHistoryItemResponse(
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
