using MovieApp.Application.Models.TvShows;
using MovieApp.Application.Services.TvShows;
using MovieApp.Domain.Entities;
using MovieApp.Domain.Enums;

namespace MovieApp.Application.Mapping;

public static class TvShowMapper
{
    public static TvShowSearchResult ToSearchResult(TvShow tvShow) =>
        new(
            tvShow.Id,
            tvShow.TmdbId,
            tvShow.TvdbId,
            tvShow.ImdbId,
            tvShow.Title,
            tvShow.OriginalTitle,
            tvShow.Overview,
            tvShow.FirstAirDate,
            tvShow.PosterPath,
            tvShow.BackdropPath,
            tvShow.OriginalLanguage,
            tvShow.VoteAverage,
            tvShow.VoteCount);

    public static TvShowDetailsResult ToDetailsResult(TvShow tvShow) =>
        new(
            tvShow.Id,
            tvShow.TmdbId,
            tvShow.TvdbId,
            tvShow.ImdbId,
            tvShow.Title,
            tvShow.OriginalTitle,
            tvShow.Overview,
            tvShow.FirstAirDate,
            tvShow.LastAirDate,
            tvShow.PosterPath,
            tvShow.BackdropPath,
            tvShow.OriginalLanguage,
            tvShow.VoteAverage,
            tvShow.VoteCount,
            ToStatusLabel(tvShow.Status),
            tvShow.TvShowGenres
                .Select(tvShowGenre => tvShowGenre.Genre.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList(),
            tvShow.Seasons
                .OrderBy(season => season.SeasonNumber)
                .Select(ToSeasonSummaryResult)
                .ToList(),
            TvShowFollowActionEligibility.CanFollow(tvShow.Status));

    public static SeasonSummaryResult ToSeasonSummaryResult(Season season) =>
        new(
            season.Id,
            season.SeasonNumber,
            season.Name,
            season.AirDate,
            season.EpisodeCount,
            season.PosterPath);

    public static SeasonResult ToSeasonResult(Season season) =>
        new(
            season.Id,
            season.TvShowId,
            season.SeasonNumber,
            season.Name,
            season.Overview,
            season.AirDate,
            season.EpisodeCount,
            season.PosterPath,
            season.Episodes
                .OrderBy(episode => episode.EpisodeNumber)
                .Select(ToEpisodeSummaryResult)
                .ToList());

    public static EpisodeSummaryResult ToEpisodeSummaryResult(Episode episode) =>
        new(
            episode.Id,
            episode.EpisodeNumber,
            episode.Name,
            episode.AirDate,
            episode.RuntimeMinutes,
            episode.StillPath,
            episode.VoteAverage,
            episode.VoteCount);

    public static EpisodeResult ToEpisodeResult(Episode episode, Guid tvShowId, int seasonNumber) =>
        new(
            episode.Id,
            tvShowId,
            episode.SeasonId,
            seasonNumber,
            episode.EpisodeNumber,
            episode.Name,
            episode.Overview,
            episode.AirDate,
            episode.RuntimeMinutes,
            episode.StillPath,
            episode.VoteAverage,
            episode.VoteCount);

    public static string ToStatusLabel(TvShowStatus status) =>
        status switch
        {
            TvShowStatus.ReturningSeries => "Returning Series",
            TvShowStatus.InProduction => "In Production",
            TvShowStatus.Ended => "Ended",
            TvShowStatus.Canceled => "Canceled",
            TvShowStatus.Pilot => "Pilot",
            _ => "Planned"
        };
}
