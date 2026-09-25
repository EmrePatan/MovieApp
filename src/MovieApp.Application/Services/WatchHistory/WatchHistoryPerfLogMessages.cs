using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.WatchHistory;

internal static partial class WatchHistoryPerfLogMessages
{
    [LoggerMessage(
        EventId = 8201,
        Level = LogLevel.Debug,
        Message = "WatchHistoryPerf TvShowHydration TvShowId={TvShowId} TotalMs={TotalMs} SummaryHydrationMs={SummaryHydrationMs} SeasonLookupMs={SeasonLookupMs} SeasonsNeedingHydration={SeasonsNeedingHydration} SeasonsHydrated={SeasonsHydrated}")]
    public static partial void LogTvShowHydration(
        ILogger logger,
        Guid tvShowId,
        long totalMs,
        long summaryHydrationMs,
        long seasonLookupMs,
        int seasonsNeedingHydration,
        int seasonsHydrated);

    [LoggerMessage(
        EventId = 8202,
        Level = LogLevel.Debug,
        Message = "WatchHistoryPerf TvShowProgress TvShowId={TvShowId} TotalMs={TotalMs} ExistenceCheckMs={ExistenceCheckMs} ProgressQueryMs={ProgressQueryMs} DbRoundTrips={DbRoundTrips}")]
    public static partial void LogTvShowProgress(
        ILogger logger,
        Guid tvShowId,
        long totalMs,
        long existenceCheckMs,
        long progressQueryMs,
        int dbRoundTrips);

    [LoggerMessage(
        EventId = 8203,
        Level = LogLevel.Information,
        Message = "WatchHistoryPerf TvWatchStatePerf TvShowId={TvShowId} CorrelationId={CorrelationId} Watched={Watched} TotalMs={TotalMs} PreparationMs={PreparationMs} PreparationRoundTrips={PreparationRoundTrips} IngestionRequired={IngestionRequired} RegularSeasonCount={RegularSeasonCount} SeasonsWithEpisodeRowsCount={SeasonsWithEpisodeRowsCount} SeasonsMissingEpisodesCount={SeasonsMissingEpisodesCount} MissingSeasonNumbers={MissingSeasonNumbers} EnsureIngestedMs={EnsureIngestedMs} EpisodeCount={EpisodeCount} BulkWriteMs={BulkWriteMs} AffectedCount={AffectedCount} AnalyticsDispatchMs={AnalyticsDispatchMs}")]
    public static partial void LogTvWatchState(
        ILogger logger,
        Guid tvShowId,
        string? correlationId,
        bool watched,
        long totalMs,
        long preparationMs,
        int preparationRoundTrips,
        bool ingestionRequired,
        int regularSeasonCount,
        int seasonsWithEpisodeRowsCount,
        int seasonsMissingEpisodesCount,
        string missingSeasonNumbers,
        long ensureIngestedMs,
        int episodeCount,
        long bulkWriteMs,
        int affectedCount,
        long analyticsDispatchMs);

    [LoggerMessage(
        EventId = 8206,
        Level = LogLevel.Information,
        Message = "WatchHistoryPerf TvWatchStateIngestion TvShowId={TvShowId} TotalMs={TotalMs} SeasonSummaryMs={SeasonSummaryMs} SeasonLookupMs={SeasonLookupMs} ProviderSeasonFetchWallMs={ProviderSeasonFetchWallMs} ProviderSeasonFetchAccumulatedMs={ProviderSeasonFetchAccumulatedMs} MaxSeasonProviderFetchMs={MaxSeasonProviderFetchMs} PersistenceMs={PersistenceMs} SaveChangesCount={SaveChangesCount} ProviderSeasonFetchCount={ProviderSeasonFetchCount} SeasonsNeedingHydration={SeasonsNeedingHydration} SeasonsHydrated={SeasonsHydrated}")]
    public static partial void LogTvWatchStateIngestion(
        ILogger logger,
        Guid tvShowId,
        long totalMs,
        long seasonSummaryMs,
        long seasonLookupMs,
        long providerSeasonFetchWallMs,
        long providerSeasonFetchAccumulatedMs,
        long maxSeasonProviderFetchMs,
        long persistenceMs,
        int saveChangesCount,
        int providerSeasonFetchCount,
        int seasonsNeedingHydration,
        int seasonsHydrated);

    [LoggerMessage(
        EventId = 8204,
        Level = LogLevel.Information,
        Message = "WatchHistoryPerf MovieWatchStatePerf MovieId={MovieId} CorrelationId={CorrelationId} TotalMs={TotalMs} EnsureMovieExistsMs={EnsureMovieExistsMs} UpsertMs={UpsertMs} Created={Created} AnalyticsDispatchMs={AnalyticsDispatchMs}")]
    public static partial void LogMovieWatchState(
        ILogger logger,
        Guid movieId,
        string? correlationId,
        long totalMs,
        long ensureMovieExistsMs,
        long upsertMs,
        bool created,
        long analyticsDispatchMs);
}
