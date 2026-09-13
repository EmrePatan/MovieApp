namespace MovieApp.Contracts.WatchHistory;

public sealed record MarkThroughEpisodeResponse(
    Guid EpisodeId,
    int AffectedCount,
    DateTime WatchedAt);
