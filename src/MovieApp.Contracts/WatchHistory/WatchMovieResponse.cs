namespace MovieApp.Contracts.WatchHistory;

public sealed record WatchMovieResponse(Guid MovieId, DateTime WatchedAt);
