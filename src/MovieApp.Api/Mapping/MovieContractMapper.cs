using MovieApp.Application.Models.Movies;
using MovieApp.Contracts.Movies;

namespace MovieApp.Api.Mapping;

public static class MovieContractMapper
{
    public static MovieSearchResponse ToSearchResponse(PaginatedResult<MovieSearchResult> result) =>
        new(
            result.Items.Select(ToSearchItemResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);

    public static MovieSearchItemResponse ToSearchItemResponse(MovieSearchResult result) =>
        new(
            result.Id,
            new ExternalIdsResponse(result.TmdbId, result.TvdbId, result.ImdbId),
            result.Title,
            result.Overview,
            result.ReleaseDate,
            result.PosterPath,
            result.VoteAverage,
            result.VoteCount);

    public static MovieDetailsResponse ToDetailsResponse(MovieDetailsResult result) =>
        new(
            result.Id,
            new ExternalIdsResponse(result.TmdbId, result.TvdbId, result.ImdbId),
            result.Title,
            result.OriginalTitle,
            result.Overview,
            result.ReleaseDate,
            result.RuntimeMinutes,
            result.PosterPath,
            result.BackdropPath,
            result.OriginalLanguage,
            result.VoteAverage,
            result.VoteCount,
            result.Genres,
            result.Collection is null
                ? null
                : new MovieCollectionSummaryResponse(
                    result.Collection.TmdbId,
                    result.Collection.Name,
                    result.Collection.PosterPath,
                    result.Collection.BackdropPath),
            result.IsReleased);
}
