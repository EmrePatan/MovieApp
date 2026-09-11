namespace MovieApp.Contracts.Watchlists;

public sealed record WatchlistMovieItemResponse(
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? ReleaseDate,
    decimal VoteAverage,
    DateTime CreatedAt);
