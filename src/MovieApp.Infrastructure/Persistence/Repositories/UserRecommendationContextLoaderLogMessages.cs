using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static partial class UserRecommendationContextLoaderLogMessages
{
    [LoggerMessage(
        EventId = 7200,
        Level = LogLevel.Debug,
        Message = "RecHomePerf UserContext TotalMs={TotalMs} DbTotalMs={DbTotalMs} CpuMs={CpuMs} ProbeMs={ProbeMs} DbRoundTrips={DbRoundTrips} InitialQueriesExecutionMode=probe-short-circuit SignalBuildExecutionMode=skipped MeaningfulInteractionCount={MeaningfulInteractionCount}")]
    public static partial void LogPreflightShortCircuit(
        ILogger logger,
        long totalMs,
        long dbTotalMs,
        long cpuMs,
        long probeMs,
        int meaningfulInteractionCount,
        int dbRoundTrips);

    [LoggerMessage(
        EventId = 7201,
        Level = LogLevel.Debug,
        Message = "RecHomePerf UserContext TotalMs={TotalMs} DbTotalMs={DbTotalMs} CpuMs={CpuMs} ProbeMs={ProbeMs} DbRoundTrips={DbRoundTrips} InitialQueriesExecutionMode={InitialQueriesExecutionMode} RatingsMs={RatingsMs} FavoritesMs={FavoritesMs} WatchedMoviesMs={WatchedMoviesMs} WatchlistMs={WatchlistMs} WatchedEpisodesMs={WatchedEpisodesMs} CatalogFollowsMs={CatalogFollowsMs} SearchHistoryMs={SearchHistoryMs} FullyWatchedTvMs={FullyWatchedTvMs} WatchedTvTitlesMs={WatchedTvTitlesMs} TvFollowTitlesMs={TvFollowTitlesMs} SearchMatchMoviesMs={SearchMatchMoviesMs} SearchMatchTvMs={SearchMatchTvMs} MovieSignalsDbMs={MovieSignalsDbMs} TvSignalsDbMs={TvSignalsDbMs} SignalBuildExecutionMode={SignalBuildExecutionMode} RatingCount={RatingCount} FavoriteCount={FavoriteCount} WatchedMovieCount={WatchedMovieCount} WatchlistCount={WatchlistCount} WatchedEpisodeCount={WatchedEpisodeCount} CatalogFollowCount={CatalogFollowCount} SearchQueryCount={SearchQueryCount} MovieSignalCount={MovieSignalCount} TvSignalCount={TvSignalCount} MeaningfulInteractionCount={MeaningfulInteractionCount}")]
    public static partial void LogLoadComplete(
        ILogger logger,
        long totalMs,
        long dbTotalMs,
        long cpuMs,
        long probeMs,
        string initialQueriesExecutionMode,
        long ratingsMs,
        long favoritesMs,
        long watchedMoviesMs,
        long watchlistMs,
        long watchedEpisodesMs,
        long catalogFollowsMs,
        long searchHistoryMs,
        long fullyWatchedTvMs,
        long watchedTvTitlesMs,
        long tvFollowTitlesMs,
        long searchMatchMoviesMs,
        long searchMatchTvMs,
        long movieSignalsDbMs,
        long tvSignalsDbMs,
        string signalBuildExecutionMode,
        int ratingCount,
        int favoriteCount,
        int watchedMovieCount,
        int watchlistCount,
        int watchedEpisodeCount,
        int catalogFollowCount,
        int searchQueryCount,
        int movieSignalCount,
        int tvSignalCount,
        int meaningfulInteractionCount,
        int dbRoundTrips);
}
