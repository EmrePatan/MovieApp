namespace MovieApp.Application.Models.Watchlists;

public sealed record WatchlistItemTvShowResult(
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? FirstAirDate,
    decimal VoteAverage,
    DateTime CreatedAt);
