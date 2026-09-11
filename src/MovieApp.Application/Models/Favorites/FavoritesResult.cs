namespace MovieApp.Application.Models.Favorites;

public sealed record FavoritesResult(
    IReadOnlyList<FavoriteMovieResult> Movies,
    IReadOnlyList<FavoriteTvShowResult> TvShows,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public bool HasNextPage => Page < TotalPages;

    public bool HasPreviousPage => Page > 1;
}
