using MovieApp.Application.Models.Collections;
using MovieApp.Application.Models.Movies;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Mapping;

public static class MovieMapper
{
    public static MovieSearchResult ToSearchResult(Movie movie) =>
        new(
            movie.Id,
            movie.TmdbId,
            movie.TvdbId,
            movie.ImdbId,
            movie.Title,
            movie.Overview,
            movie.ReleaseDate,
            movie.PosterPath,
            movie.VoteAverage,
            movie.VoteCount);

    public static MovieDetailsResult ToDetailsResult(Movie movie) =>
        new(
            movie.Id,
            movie.TmdbId,
            movie.TvdbId,
            movie.ImdbId,
            movie.Title,
            movie.OriginalTitle,
            movie.Overview,
            movie.ReleaseDate,
            movie.RuntimeMinutes,
            movie.PosterPath,
            movie.BackdropPath,
            movie.OriginalLanguage,
            movie.VoteAverage,
            movie.VoteCount,
            movie.MovieGenres
                .Select(movieGenre => movieGenre.Genre.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList(),
            ToCollectionSummary(movie),
            IsReleased: true,
            CanFollowForRelease: true,
            CanSetReleaseAlert: true);

    private static MovieCollectionSummaryResult? ToCollectionSummary(Movie movie)
    {
        if (!movie.TmdbCollectionId.HasValue || string.IsNullOrWhiteSpace(movie.CollectionName))
        {
            return null;
        }

        return new MovieCollectionSummaryResult(
            movie.TmdbCollectionId.Value,
            movie.CollectionName,
            movie.CollectionPosterPath,
            movie.CollectionBackdropPath);
    }
}
