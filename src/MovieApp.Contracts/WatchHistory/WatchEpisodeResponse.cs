namespace MovieApp.Contracts.WatchHistory;

public sealed record WatchEpisodeResponse(Guid EpisodeId, DateTime WatchedAt);
