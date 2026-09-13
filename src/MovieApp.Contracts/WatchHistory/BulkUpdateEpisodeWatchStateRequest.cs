namespace MovieApp.Contracts.WatchHistory;

public sealed record BulkUpdateEpisodeWatchStateRequest(
    IReadOnlyList<Guid> EpisodeIds,
    bool Watched);
