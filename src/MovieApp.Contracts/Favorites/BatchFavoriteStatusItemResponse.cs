namespace MovieApp.Contracts.Favorites;

public sealed record BatchFavoriteStatusItemResponse(string ContentType, Guid Id, bool IsFavorited);
