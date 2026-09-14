namespace MovieApp.Contracts.Favorites;

public sealed record BatchFavoriteStatusResponse(
    IReadOnlyList<BatchFavoriteStatusItemResponse> Items);
