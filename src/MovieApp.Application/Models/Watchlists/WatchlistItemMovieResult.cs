namespace MovieApp.Application.Models.Watchlists;

public sealed record WatchlistItemMovieResult(
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? ReleaseDate,
    decimal VoteAverage,
    DateTime CreatedAt);
