using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.WatchHistory;

namespace MovieApp.Application.Services.WatchHistory;

public interface IWatchHistoryService
{
    Task<WatchMutationResult> MarkMovieWatchedAsync(Guid movieId, CancellationToken cancellationToken = default);

    Task UnmarkMovieWatchedAsync(Guid movieId, CancellationToken cancellationToken = default);

    Task<MovieWatchStatusResult> GetMovieWatchStatusAsync(Guid movieId, CancellationToken cancellationToken = default);

    Task<PaginatedResult<WatchedMovieResult>> GetWatchedMoviesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<WatchMutationResult> MarkEpisodeWatchedAsync(Guid episodeId, CancellationToken cancellationToken = default);

    Task UnmarkEpisodeWatchedAsync(Guid episodeId, CancellationToken cancellationToken = default);

    Task<EpisodeWatchStatusResult> GetEpisodeWatchStatusAsync(Guid episodeId, CancellationToken cancellationToken = default);

    Task<PaginatedResult<WatchedEpisodeResult>> GetWatchedEpisodesAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<RecentWatchHistoryItemResult>> GetRecentWatchHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<TvShowWatchProgressResult> GetTvShowWatchProgressAsync(
        Guid tvShowId,
        CancellationToken cancellationToken = default);

    Task<SeasonWatchProgressResult> GetSeasonWatchProgressAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContinueWatchingItemResult>> GetContinueWatchingAsync(
        int sectionSize,
        CancellationToken cancellationToken = default);

    Task<SeasonWatchedEpisodesResult> GetSeasonWatchedEpisodesAsync(
        Guid tvShowId,
        int seasonNumber,
        CancellationToken cancellationToken = default);

    Task<BulkUpdateEpisodeWatchStateResult> BulkUpdateEpisodeWatchStateAsync(
        Guid tvShowId,
        IReadOnlyList<Guid> episodeIds,
        bool watched,
        CancellationToken cancellationToken = default);

    Task<MarkThroughEpisodeResult> MarkThroughEpisodeAsync(
        Guid tvShowId,
        Guid episodeId,
        CancellationToken cancellationToken = default);

    Task<BulkUpdateEpisodeWatchStateResult> BulkUpdateSeasonWatchStateAsync(
        Guid tvShowId,
        int seasonNumber,
        bool watched,
        CancellationToken cancellationToken = default);

    Task<BulkUpdateEpisodeWatchStateResult> BulkUpdateTvShowWatchStateAsync(
        Guid tvShowId,
        bool watched,
        CancellationToken cancellationToken = default);
}
