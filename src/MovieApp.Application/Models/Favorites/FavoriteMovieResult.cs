namespace MovieApp.Application.Models.Favorites;

public sealed record FavoriteMovieResult(
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? ReleaseDate,
    decimal VoteAverage);
