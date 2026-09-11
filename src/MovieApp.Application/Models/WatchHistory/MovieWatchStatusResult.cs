namespace MovieApp.Application.Models.WatchHistory;

public sealed record MovieWatchStatusResult(Guid MovieId, bool IsWatched, DateTime? WatchedAt);
