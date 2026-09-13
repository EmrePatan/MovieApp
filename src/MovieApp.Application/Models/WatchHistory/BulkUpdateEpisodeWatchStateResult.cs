namespace MovieApp.Application.Models.WatchHistory;

public sealed record BulkUpdateEpisodeWatchStateResult(int AffectedCount, DateTime? WatchedAt);
