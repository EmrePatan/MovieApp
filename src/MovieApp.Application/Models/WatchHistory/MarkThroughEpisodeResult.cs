namespace MovieApp.Application.Models.WatchHistory;

public sealed record MarkThroughEpisodeResult(
    Guid EpisodeId,
    int AffectedCount,
    DateTime WatchedAt);
