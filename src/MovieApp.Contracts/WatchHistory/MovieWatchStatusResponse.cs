namespace MovieApp.Contracts.WatchHistory;

public sealed record MovieWatchStatusResponse(Guid MovieId, bool IsWatched, DateTime? WatchedAt);
