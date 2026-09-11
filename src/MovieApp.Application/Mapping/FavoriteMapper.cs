using MovieApp.Application.Models.Favorites;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Mapping;

public static class FavoriteMapper
{
    public static FavoriteMovieResult ToMovieResult(Movie movie) =>
        new(
            movie.Id,
            movie.Title,
            movie.PosterPath,
            movie.ReleaseDate,
            movie.VoteAverage);

    public static FavoriteTvShowResult ToTvShowResult(TvShow tvShow) =>
        new(
            tvShow.Id,
            tvShow.Title,
            tvShow.PosterPath,
            tvShow.FirstAirDate,
            tvShow.VoteAverage);

    public static FavoritesResult ToFavoritesResult(
        IReadOnlyList<Favorite> favorites,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var movies = favorites
            .Where(favorite => favorite.Movie is not null)
            .Select(favorite => ToMovieResult(favorite.Movie!))
            .ToList();

        var tvShows = favorites
            .Where(favorite => favorite.TvShow is not null)
            .Select(favorite => ToTvShowResult(favorite.TvShow!))
            .ToList();

        return new FavoritesResult(movies, tvShows, page, pageSize, totalCount, totalPages);
    }
}
