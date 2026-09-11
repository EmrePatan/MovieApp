namespace MovieApp.Contracts.Favorites;

public sealed record FavoritesResponse(
    IReadOnlyList<FavoriteMovieItemResponse> Movies,
    IReadOnlyList<FavoriteTvShowItemResponse> TvShows,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
