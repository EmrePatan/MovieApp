namespace MovieApp.Contracts.Watchlists;

public sealed record WatchlistCatalogItemResponse(
    string ContentType,
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? ReleaseDate,
    DateOnly? FirstAirDate,
    decimal VoteAverage,
    DateTime CreatedAt);
