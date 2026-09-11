using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.TvShows;
using MovieApp.Contracts.Movies;
using MovieApp.Contracts.TvShows;

namespace MovieApp.Api.Mapping;

public static class TvShowContractMapper
{
    public static TvShowSearchResponse ToSearchResponse(PaginatedResult<TvShowSearchResult> result) =>
        new(
            result.Items.Select(ToSearchItemResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);

    public static TvShowSearchItemResponse ToSearchItemResponse(TvShowSearchResult result) =>
        new(
            result.Id,
            new ExternalIdsResponse(result.TmdbId, result.TvdbId, result.ImdbId),
            result.Title,
            result.OriginalTitle,
            result.Overview,
            result.FirstAirDate,
            result.PosterPath,
            result.BackdropPath,
            result.OriginalLanguage,
            result.VoteAverage,
            result.VoteCount);

    public static TvShowDetailsResponse ToDetailsResponse(TvShowDetailsResult result) =>
        new(
            result.Id,
            new ExternalIdsResponse(result.TmdbId, result.TvdbId, result.ImdbId),
            result.Title,
            result.OriginalTitle,
            result.Overview,
            result.FirstAirDate,
            result.LastAirDate,
            result.PosterPath,
            result.BackdropPath,
            result.OriginalLanguage,
            result.VoteAverage,
            result.VoteCount,
            result.Status,
            result.Genres,
            result.Seasons.Select(ToSeasonSummaryResponse).ToList());

    public static SeasonSummaryResponse ToSeasonSummaryResponse(SeasonSummaryResult result) =>
        new(
            result.Id,
            result.SeasonNumber,
            result.Name,
            result.AirDate,
            result.EpisodeCount,
            result.PosterPath);

    public static SeasonResponse ToSeasonResponse(SeasonResult result) =>
        new(
            result.Id,
            result.TvShowId,
            result.SeasonNumber,
            result.Name,
            result.Overview,
            result.AirDate,
            result.EpisodeCount,
            result.PosterPath,
            result.Episodes.Select(ToEpisodeSummaryResponse).ToList());

    public static EpisodeSummaryResponse ToEpisodeSummaryResponse(EpisodeSummaryResult result) =>
        new(
            result.Id,
            result.EpisodeNumber,
            result.Name,
            result.AirDate,
            result.RuntimeMinutes,
            result.StillPath,
            result.VoteAverage,
            result.VoteCount);

    public static EpisodeResponse ToEpisodeResponse(EpisodeResult result) =>
        new(
            result.Id,
            result.TvShowId,
            result.SeasonId,
            result.SeasonNumber,
            result.EpisodeNumber,
            result.Name,
            result.Overview,
            result.AirDate,
            result.RuntimeMinutes,
            result.StillPath,
            result.VoteAverage,
            result.VoteCount);
}
