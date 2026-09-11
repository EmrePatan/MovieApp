namespace MovieApp.Contracts.WatchHistory;

public sealed record EpisodeWatchStatusResponse(Guid EpisodeId, bool IsWatched, DateTime? WatchedAt);
