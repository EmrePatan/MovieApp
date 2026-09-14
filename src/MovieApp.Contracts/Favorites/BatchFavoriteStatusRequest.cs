namespace MovieApp.Contracts.Favorites;

public sealed record BatchFavoriteStatusRequest(IReadOnlyList<BatchFavoriteStatusItemRequest> Items);
