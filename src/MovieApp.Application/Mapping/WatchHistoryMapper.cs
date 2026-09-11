using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Mapping;

public static class WatchHistoryMapper
{
    public static WatchedMovieResult ToWatchedMovieResult(WatchedMovie watchedMovie) =>
        new(
            watchedMovie.MovieId,
            watchedMovie.Movie.Title,
            watchedMovie.WatchedAt);

    public static WatchedEpisodeResult ToWatchedEpisodeResult(WatchedEpisode watchedEpisode) =>
        new(
            watchedEpisode.EpisodeId,
            watchedEpisode.Episode.Season.TvShowId,
            watchedEpisode.Episode.SeasonId,
            watchedEpisode.Episode.Season.TvShow.Title,
            watchedEpisode.Episode.Season.SeasonNumber,
            watchedEpisode.Episode.EpisodeNumber,
            watchedEpisode.Episode.Name,
            watchedEpisode.WatchedAt);

    public static PaginatedResult<WatchedMovieResult> ToWatchedMoviesResult(
        IReadOnlyList<WatchedMovie> items,
        int page,
        int pageSize,
        int totalCount) =>
        new(
            items.Select(ToWatchedMovieResult).ToList(),
            page,
            pageSize,
            totalCount,
            CalculateTotalPages(totalCount, pageSize));

    public static PaginatedResult<WatchedEpisodeResult> ToWatchedEpisodesResult(
        IReadOnlyList<WatchedEpisode> items,
        int page,
        int pageSize,
        int totalCount) =>
        new(
            items.Select(ToWatchedEpisodeResult).ToList(),
            page,
            pageSize,
            totalCount,
            CalculateTotalPages(totalCount, pageSize));

    public static PaginatedResult<RecentWatchHistoryItemResult> ToRecentWatchHistoryResult(
        IReadOnlyList<RecentWatchHistoryItemResult> items,
        int page,
        int pageSize,
        int totalCount) =>
        new(items, page, pageSize, totalCount, CalculateTotalPages(totalCount, pageSize));

    public static NextEpisodeResult? ToNextEpisodeResult(Episode? episode) =>
        episode is null
            ? null
            : new NextEpisodeResult(
                episode.Id,
                episode.Season.SeasonNumber,
                episode.EpisodeNumber,
                episode.Name);

    public static SeasonNextEpisodeResult? ToSeasonNextEpisodeResult(Episode? episode) =>
        episode is null
            ? null
            : new SeasonNextEpisodeResult(episode.Id, episode.EpisodeNumber, episode.Name);

    public static decimal CalculateProgressPercentage(int watchedEpisodes, int totalEpisodes) =>
        totalEpisodes == 0
            ? 0m
            : Math.Round(watchedEpisodes / (decimal)totalEpisodes * 100m, 2, MidpointRounding.AwayFromZero);

    private static int CalculateTotalPages(int totalCount, int pageSize) =>
        totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
}
