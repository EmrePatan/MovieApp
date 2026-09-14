using Microsoft.Extensions.Logging;

namespace MovieApp.Api.BackgroundJobs;

internal static partial class BackgroundJobLogMessages
{
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "TMDB TV changes sync completed: windows={WindowsProcessed} changedIds={ChangedIds} refreshedShows={RefreshedShows} lastEndDate={LastEndDate}")]
    internal static partial void LogTmdbTvChangesSyncCompleted(
        ILogger logger,
        int windowsProcessed,
        int changedIds,
        int refreshedShows,
        DateOnly? lastEndDate);

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
}
