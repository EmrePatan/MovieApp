namespace MovieApp.Application.Models.WatchHistory;

public sealed record EpisodeWatchStatusResult(Guid EpisodeId, bool IsWatched, DateTime? WatchedAt);
