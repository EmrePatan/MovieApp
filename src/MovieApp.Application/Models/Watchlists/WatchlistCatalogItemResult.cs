namespace MovieApp.Application.Models.Watchlists;

public sealed record WatchlistCatalogItemResult(
    string ContentType,
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? ReleaseDate,
    DateOnly? FirstAirDate,
    decimal VoteAverage,
    DateTime CreatedAt);
