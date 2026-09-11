namespace MovieApp.Contracts.Favorites;

public sealed record FavoriteMovieItemResponse(
    Guid Id,
    string Title,
    string? PosterPath,
    DateOnly? ReleaseDate,
    decimal VoteAverage);
