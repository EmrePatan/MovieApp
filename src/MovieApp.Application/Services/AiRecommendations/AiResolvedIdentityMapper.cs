using MovieApp.Application.Models.AiRecommendations;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Services.AiRecommendations;

internal static class AiResolvedIdentityMapper
{
    internal static ResolvedMovieIdentity FromMovie(Movie movie) =>
        new(
            "movie",
            movie.Id,
            movie.TmdbId,
            movie.Title,
            movie.ReleaseDate?.Year,
            movie.RuntimeMinutes,
            movie.OriginalTitle,
            movie.Overview,
            movie.PosterPath,
            movie.BackdropPath,
            movie.ReleaseDate,
            movie.VoteAverage,
            movie.VoteCount,
            movie.MovieGenres
                .Select(movieGenre => movieGenre.Genre.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList());

    internal static ResolvedMovieIdentity FromTvShow(TvShow tvShow) =>
        new(
            "tv",
            tvShow.Id,
            tvShow.TmdbId,
            tvShow.Title,
            tvShow.FirstAirDate?.Year,
            null,
            tvShow.OriginalTitle,
            tvShow.Overview,
            tvShow.PosterPath,
            tvShow.BackdropPath,
            tvShow.FirstAirDate,
            tvShow.VoteAverage,
            tvShow.VoteCount,
            tvShow.TvShowGenres
                .Select(tvShowGenre => tvShowGenre.Genre.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList());
}
