namespace MovieApp.Application.Models.Favorites;

public sealed record FavoriteTvShowResult(
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? FirstAirDate,
    decimal VoteAverage);
