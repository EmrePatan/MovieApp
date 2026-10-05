using Microsoft.Extensions.Logging;

namespace MovieApp.Api.BackgroundJobs;

internal static partial class BackgroundJobLogMessages
{
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Information,
        Message = "Background job started: jobId={JobId}")]
    internal static partial void LogBackgroundJobStarted(ILogger logger, string jobId);

    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "TMDB TV changes sync completed: windows={WindowsProcessed} changedIds={ChangedIds} existingUserRelevant={ExistingUserRelevant} discoveryRelevant={DiscoveryRelevant} discoveryOnlyRelevant={DiscoveryOnlyRelevant} unionRelevant={UnionRelevant} refreshed={Refreshed} skipped={Skipped} failed={Failed} lastEndDate={LastEndDate}")]
    internal static partial void LogTmdbTvChangesSyncCompleted(
        ILogger logger,
        int windowsProcessed,
        int changedIds,
        int existingUserRelevant,
        int discoveryRelevant,
        int discoveryOnlyRelevant,
        int unionRelevant,
        int refreshed,
        int skipped,
        int failed,
        DateOnly? lastEndDate);

    [LoggerMessage(
        EventId = 6014,
        Level = LogLevel.Information,
        Message = "TMDB movie changes sync completed: windows={WindowsProcessed} changedIds={ChangedIds} existingUserRelevant={ExistingUserRelevant} discoveryRelevant={DiscoveryRelevant} discoveryOnlyRelevant={DiscoveryOnlyRelevant} unionRelevant={UnionRelevant} refreshed={Refreshed} skipped={Skipped} failed={Failed} lastEndDate={LastEndDate}")]
    internal static partial void LogTmdbMovieChangesSyncCompleted(
        ILogger logger,
        int windowsProcessed,
        int changedIds,
        int existingUserRelevant,
        int discoveryRelevant,
        int discoveryOnlyRelevant,
        int unionRelevant,
        int refreshed,
        int skipped,
        int failed,
        DateOnly? lastEndDate);

    [LoggerMessage(
        EventId = 6025,
        Level = LogLevel.Information,
        Message = "Catalog metadata freshness safety-net completed: eligibleStale={EligibleStale} selected={Selected} moviesSelected={MoviesSelected} tvSelected={TvSelected} refreshed={Refreshed} skipped={Skipped} failed={Failed} remainingEstimate={RemainingEstimate} durationMs={DurationMs}")]
    internal static partial void LogCatalogMetadataFreshnessSafetyNetCompleted(
        ILogger logger,
        int eligibleStale,
        int selected,
        int moviesSelected,
        int tvSelected,
        int refreshed,
        int skipped,
        int failed,
        int remainingEstimate,
        long durationMs);

    [LoggerMessage(
        EventId = 6026,
        Level = LogLevel.Information,
        Message = "Catalog metadata freshness distribution: relevantTotal={RelevantTotal} fresh24h={Fresh24h} fresh24to72h={Fresh24To72h} staleOver72h={StaleOver72h} staleOver7d={StaleOver7d} neverRefreshed={NeverRefreshed} freshWithin72hPercent={FreshWithin72hPercent}")]
    internal static partial void LogCatalogMetadataFreshnessDistribution(
        ILogger logger,
        int relevantTotal,
        int fresh24h,
        int fresh24To72h,
        int staleOver72h,
        int staleOver7d,
        int neverRefreshed,
        decimal freshWithin72hPercent);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "Hot release check completed: boundary={BoundaryDate} candidates={Candidates} checked={CheckedCount} hydrated={Hydrated} events={Events} failures={Failures}")]
    internal static partial void LogHotReleaseCheckCompleted(
        ILogger logger,
        DateOnly boundaryDate,
        int candidates,
        int checkedCount,
        int hydrated,
        int events,
        int failures);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Information,
        Message = "Release notification fanout completed: no pending events")]
    internal static partial void LogReleaseNotificationFanoutNoPendingEvents(ILogger logger);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Information,
        Message = "Release notification fanout completed: events={EventsProcessed} discovered={Discovered} notifications={NotificationsCreated} links={LinksCreated} skippedPreference={SkippedPreference} skippedBoundary={SkippedBoundary} skippedSource={SkippedSource}")]
    internal static partial void LogReleaseNotificationFanoutCompleted(
        ILogger logger,
        int eventsProcessed,
        int discovered,
        int notificationsCreated,
        int linksCreated,
        int skippedPreference,
        int skippedBoundary,
        int skippedSource);

    [LoggerMessage(
        EventId = 6005,
        Level = LogLevel.Information,
        Message = "Push delivery preparation completed: no pending notifications")]
    internal static partial void LogPushDeliveryPreparationNoPendingNotifications(ILogger logger);

    [LoggerMessage(
        EventId = 6006,
        Level = LogLevel.Information,
        Message = "Push delivery preparation completed: discovered={Discovered} notifications={NotificationsProcessed} deliveriesCreated={DeliveriesCreated}")]
    internal static partial void LogPushDeliveryPreparationCompleted(
        ILogger logger,
        int discovered,
        int notificationsProcessed,
        int deliveriesCreated);

    [LoggerMessage(
        EventId = 6007,
        Level = LogLevel.Information,
        Message = "Push dispatch completed: claimed={Claimed} sent={Sent} skipped={Skipped} retryableFailures={RetryableFailures} permanentFailures={PermanentFailures}")]
    internal static partial void LogPushDispatchCompleted(
        ILogger logger,
        int claimed,
        int sent,
        int skipped,
        int retryableFailures,
        int permanentFailures);

    [LoggerMessage(
        EventId = 6008,
        Level = LogLevel.Information,
        Message = "Push receipt processing completed: processed={Processed} delivered={Delivered} retryableFailures={RetryableFailures} permanentFailures={PermanentFailures}")]
    internal static partial void LogPushReceiptProcessingCompleted(
        ILogger logger,
        int processed,
        int delivered,
        int retryableFailures,
        int permanentFailures);

    [LoggerMessage(
        EventId = 6009,
        Level = LogLevel.Information,
        Message = "Movie release check completed: checked={MoviesChecked} events={ReleaseEventsCreated} providerFailures={SkippedProviderFailures} notReleased={SkippedNotReleased}")]
    internal static partial void LogMovieReleaseCheckCompleted(
        ILogger logger,
        int moviesChecked,
        int releaseEventsCreated,
        int skippedProviderFailures,
        int skippedNotReleased);

    [LoggerMessage(
        EventId = 6018,
        Level = LogLevel.Information,
        Message = "Catalog keyword backfill is disabled; skipping execution.")]
    internal static partial void LogCatalogKeywordBackfillDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 6020,
        Level = LogLevel.Information,
        Message = "MDBList keyword backfill is disabled; skipping execution.")]
    internal static partial void LogMdbListKeywordBackfillDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 6021,
        Level = LogLevel.Information,
        Message = "MDBList keyword backfill completed: no eligible candidates.")]
    internal static partial void LogMdbListKeywordBackfillNoCandidates(ILogger logger);

    [LoggerMessage(
        EventId = 6022,
        Level = LogLevel.Information,
        Message = "MDBList keyword backfill completed: selected={Selected} succeeded={Succeeded} failed={Failed} skipped={Skipped} movies={MoviesProcessed} tv={TvShowsProcessed} durationMs={DurationMs}")]
    internal static partial void LogMdbListKeywordBackfillCompleted(
        ILogger logger,
        int selected,
        int succeeded,
        int failed,
        int skipped,
        int moviesProcessed,
        int tvShowsProcessed,
        long durationMs);

    [LoggerMessage(
        EventId = 6010,
        Level = LogLevel.Information,
        Message = "Catalog keyword backfill completed: no eligible candidates. coverage={Synced}/{Eligible} ({CoveragePercent}%)")]
    internal static partial void LogCatalogKeywordBackfillNoCandidates(
        ILogger logger,
        int synced,
        int eligible,
        decimal coveragePercent);

    [LoggerMessage(
        EventId = 6011,
        Level = LogLevel.Information,
        Message = "Catalog keyword backfill completed: selected={Selected} succeeded={Succeeded} failed={Failed} skipped={Skipped} movies={MoviesProcessed} tv={TvShowsProcessed} durationMs={DurationMs} coverageBefore={CoverageBeforePercent}% coverageAfter={CoverageAfterPercent}%")]
    internal static partial void LogCatalogKeywordBackfillCompleted(
        ILogger logger,
        int selected,
        int succeeded,
        int failed,
        int skipped,
        int moviesProcessed,
        int tvShowsProcessed,
        long durationMs,
        decimal coverageBeforePercent,
        decimal coverageAfterPercent);

    [LoggerMessage(
        EventId = 6012,
        Level = LogLevel.Information,
        Message = "TV upcoming episode sync completed: selected={Selected} succeeded={Succeeded} failed={Failed} hydrated={Hydrated}")]
    internal static partial void LogTvUpcomingEpisodeSyncCompleted(
        ILogger logger,
        int selected,
        int succeeded,
        int failed,
        int hydrated);

    [LoggerMessage(
        EventId = 6014,
        Level = LogLevel.Information,
        Message = "Catalog genre backfill is disabled; skipping execution.")]
    internal static partial void LogCatalogGenreBackfillDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 6015,
        Level = LogLevel.Information,
        Message = "Catalog genre backfill started: movieGenreLess={MovieGenreLess} tvGenreLess={TvGenreLess} remaining={Remaining}")]
    internal static partial void LogCatalogGenreBackfillStarted(
        ILogger logger,
        int movieGenreLess,
        int tvGenreLess,
        int remaining);

    [LoggerMessage(
        EventId = 6016,
        Level = LogLevel.Information,
        Message = "Catalog genre backfill completed: no eligible candidates. remaining={Remaining}")]
    internal static partial void LogCatalogGenreBackfillNoCandidates(
        ILogger logger,
        int remaining);

    [LoggerMessage(
        EventId = 6017,
        Level = LogLevel.Information,
        Message = "Catalog genre backfill completed: batches={Batches} selected={Selected} succeeded={Succeeded} unrepairable={Unrepairable} failed={Failed} skipped={Skipped} movies={MoviesProcessed} tv={TvShowsProcessed} remaining={Remaining} durationMs={DurationMs}")]
    internal static partial void LogCatalogGenreBackfillCompleted(
        ILogger logger,
        int batches,
        int selected,
        int succeeded,
        int unrepairable,
        int failed,
        int skipped,
        int moviesProcessed,
        int tvShowsProcessed,
        int remaining,
        long durationMs);

    [LoggerMessage(
        EventId = 6013,
        Level = LogLevel.Information,
        Message = "Notification inbox cleanup completed: deleted={DeletedCount} cutoffUtc={CutoffUtc} durationMs={DurationMs}")]
    internal static partial void LogNotificationInboxCleanupCompleted(
        ILogger logger,
        int deletedCount,
        DateTime cutoffUtc,
        long durationMs);

    [LoggerMessage(
        EventId = 6095,
        Level = LogLevel.Error,
        Message = "Background job reached terminal failure: jobId={JobId} jobName={JobName} retryCount={RetryCount}")]
    internal static partial void LogBackgroundJobTerminalFailure(
        ILogger logger,
        string jobId,
        string jobName,
        int retryCount,
        Exception exception);

    [LoggerMessage(
        EventId = 6097,
        Level = LogLevel.Warning,
        Message = "Background job attempt failed: jobId={JobId}")]
    internal static partial void LogBackgroundJobFailed(
        ILogger logger,
        string jobId,
        Exception exception);

    [LoggerMessage(
        EventId = 6098,
        Level = LogLevel.Information,
        Message = "Recurring job registration skipped: jobId={JobId} reason={Reason}")]
    internal static partial void LogRecurringJobRegistrationSkipped(
        ILogger logger,
        string jobId,
        string reason);

    [LoggerMessage(
        EventId = 6099,
        Level = LogLevel.Information,
        Message = "Background jobs disabled; recurring job registration skipped")]
    internal static partial void LogBackgroundJobsDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 6015,
        Level = LogLevel.Information,
        Message = "Hot This Week trending snapshot refresh completed: snapshotUpdated={SnapshotUpdated} providerItems={ProviderItemCount} mappedItems={MappedItemCount} skippedItems={SkippedItemCount}")]
    internal static partial void LogHotThisWeekTrendingRefreshCompleted(
        ILogger logger,
        bool snapshotUpdated,
        int providerItemCount,
        int mappedItemCount,
        int skippedItemCount);

    [LoggerMessage(
        EventId = 6016,
        Level = LogLevel.Warning,
        Message = "Keyword catalog statistics refresh failed: reason={Reason} durationMs={DurationMilliseconds}")]
    internal static partial void LogKeywordCatalogStatisticsRefreshFailed(
        ILogger logger,
        string reason,
        long durationMilliseconds);
}
