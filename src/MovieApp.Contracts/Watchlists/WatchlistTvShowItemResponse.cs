namespace MovieApp.Contracts.Watchlists;

public sealed record WatchlistTvShowItemResponse(
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? FirstAirDate,
    decimal VoteAverage,
    DateTime CreatedAt);
