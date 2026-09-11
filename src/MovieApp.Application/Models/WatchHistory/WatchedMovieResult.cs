namespace MovieApp.Application.Models.WatchHistory;

public sealed record WatchedMovieResult(Guid MovieId, string Title, DateTime WatchedAt);
