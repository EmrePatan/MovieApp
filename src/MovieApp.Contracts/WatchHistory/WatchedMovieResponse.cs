namespace MovieApp.Contracts.WatchHistory;

public sealed record WatchedMovieResponse(Guid MovieId, string Title, DateTime WatchedAt);
