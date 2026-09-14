namespace MovieApp.Contracts.Favorites;

public sealed record BatchFavoriteStatusItemRequest(string ContentType, Guid Id);
