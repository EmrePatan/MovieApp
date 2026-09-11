namespace MovieApp.Contracts.Favorites;

public sealed record FavoriteTvShowItemResponse(
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? FirstAirDate,
    decimal VoteAverage);
