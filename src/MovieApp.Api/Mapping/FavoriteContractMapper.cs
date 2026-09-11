using MovieApp.Application.Models.Favorites;
using MovieApp.Contracts.Favorites;

namespace MovieApp.Api.Mapping;

public static class FavoriteContractMapper
{
    public static FavoritesResponse ToFavoritesResponse(FavoritesResult result) =>
        new(
            result.Movies.Select(ToMovieItemResponse).ToList(),
            result.TvShows.Select(ToTvShowItemResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);

    private static FavoriteMovieItemResponse ToMovieItemResponse(FavoriteMovieResult result) =>
        new(
            result.Id,
            result.Title,
            result.PosterPath,
            result.ReleaseDate,
            result.VoteAverage);

    private static FavoriteTvShowItemResponse ToTvShowItemResponse(FavoriteTvShowResult result) =>
        new(
            result.Id,
            result.Title,
            result.PosterPath,
            result.FirstAirDate,
            result.VoteAverage);
}
