namespace MovieApp.Contracts.WatchHistory;

public sealed record BulkUpdateEpisodeWatchStateResponse(
    int AffectedCount,
    DateTime? WatchedAt);
