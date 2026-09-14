using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.WatchHistory;
using MovieApp.Contracts.WatchHistory;

namespace MovieApp.Api.Mapping;

public static class WatchHistoryContractMapper
{
    public static WatchMovieResponse ToWatchMovieResponse(Guid movieId, WatchMutationResult result) =>
        new(movieId, result.WatchedAt);

    public static WatchEpisodeResponse ToWatchEpisodeResponse(Guid episodeId, WatchMutationResult result) =>
        new(episodeId, result.WatchedAt);

    public static MovieWatchStatusResponse ToMovieWatchStatusResponse(MovieWatchStatusResult result) =>
        new(result.MovieId, result.IsWatched, result.WatchedAt);

    public static EpisodeWatchStatusResponse ToEpisodeWatchStatusResponse(EpisodeWatchStatusResult result) =>
        new(result.EpisodeId, result.IsWatched, result.WatchedAt);

    public static WatchedMoviesListResponse ToWatchedMoviesListResponse(PaginatedResult<WatchedMovieResult> result) =>
        new(
            result.Items.Select(item => new WatchedMovieResponse(item.MovieId, item.Title, item.WatchedAt)).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);

    public static WatchedEpisodesListResponse ToWatchedEpisodesListResponse(
        PaginatedResult<WatchedEpisodeResult> result) =>
        new(
            result.Items.Select(item => new WatchedEpisodeResponse(
                item.EpisodeId,
                item.TvShowId,
                item.SeasonId,
                item.TvShowTitle,
                item.SeasonNumber,
                item.EpisodeNumber,
                item.EpisodeTitle,
                item.WatchedAt)).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);

    public static RecentWatchHistoryResponse ToRecentWatchHistoryResponse(
        PaginatedResult<RecentWatchHistoryItemResult> result) =>
        new(
            result.Items.Select(item => new RecentWatchHistoryItemResponse(
                item.Type,
                item.MovieId,
                item.EpisodeId,
                item.TvShowId,
                item.Title,
                item.TvShowTitle,
                item.SeasonNumber,
                item.EpisodeNumber,
                item.EpisodeTitle,
                item.WatchedAt)).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);

    public static TvShowWatchProgressResponse ToTvShowWatchProgressResponse(TvShowWatchProgressResult result) =>
        new(
            result.TvShowId,
            result.TotalEpisodes,
            result.WatchedEpisodes,
            result.ProgressPercentage,
            result.RegularTotalEpisodes,
            result.RegularWatchedEpisodes,
            result.IsFullyWatched,
            result.NextEpisode is null
                ? null
                : new NextEpisodeResponse(
                    result.NextEpisode.EpisodeId,
                    result.NextEpisode.SeasonNumber,
                    result.NextEpisode.EpisodeNumber,
                    result.NextEpisode.Title),
            result.Seasons
                .Select(season => new TvShowSeasonProgressResponse(
                    season.SeasonNumber,
                    season.TotalEpisodes,
                    season.WatchedEpisodes,
                    season.ProgressPercentage))
                .ToList());

    public static SeasonWatchProgressResponse ToSeasonWatchProgressResponse(SeasonWatchProgressResult result) =>
        new(
            result.TvShowId,
            result.SeasonNumber,
            result.TotalEpisodes,
            result.WatchedEpisodes,
            result.ProgressPercentage,
            result.NextEpisode is null
                ? null
                : new SeasonNextEpisodeResponse(
                    result.NextEpisode.EpisodeId,
                    result.NextEpisode.EpisodeNumber,
                    result.NextEpisode.Title));

    public static SeasonWatchedEpisodesResponse ToSeasonWatchedEpisodesResponse(
        SeasonWatchedEpisodesResult result) =>
        new(result.TvShowId, result.SeasonNumber, result.WatchedEpisodeIds);

    public static BulkUpdateEpisodeWatchStateResponse ToBulkUpdateEpisodeWatchStateResponse(
        BulkUpdateEpisodeWatchStateResult result) =>
        new(result.AffectedCount, result.WatchedAt);

    public static MarkThroughEpisodeResponse ToMarkThroughEpisodeResponse(MarkThroughEpisodeResult result) =>
        new(result.EpisodeId, result.AffectedCount, result.WatchedAt);
}
