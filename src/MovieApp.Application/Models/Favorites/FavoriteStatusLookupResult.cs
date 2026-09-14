namespace MovieApp.Application.Models.Favorites;

public sealed record FavoriteStatusLookupResult(string ContentType, Guid Id, bool IsFavorited);
