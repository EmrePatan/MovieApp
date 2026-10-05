using Microsoft.Extensions.Logging;
using MovieApp.Application.Models.Changes;

namespace MovieApp.Application.Services.Changes;

internal static partial class TmdbChangesSyncLogMessages
{
    [LoggerMessage(
        EventId = 7100,
        Level = LogLevel.Information,
        Message = "Skipped TMDB {MediaType} changes refresh for catalog id {CatalogId} with outcome {Outcome}.")]
    internal static partial void LogSkippedTargetRefresh(
        ILogger logger,
        string mediaType,
        Guid catalogId,
        TmdbChangesTargetRefreshOutcome outcome);

    [LoggerMessage(
        EventId = 7101,
        Level = LogLevel.Warning,
        Message = "Failed TMDB {MediaType} changes refresh for catalog id {CatalogId}.")]
    internal static partial void LogFailedTargetRefresh(
        ILogger logger,
        string mediaType,
        Guid catalogId);

    [LoggerMessage(
        EventId = 7102,
        Level = LogLevel.Warning,
        Message = "Failed TMDB {MediaType} changes refresh for catalog id {CatalogId}.")]
    internal static partial void LogFailedTargetRefresh(
        ILogger logger,
        Exception exception,
        string mediaType,
        Guid catalogId);

    [LoggerMessage(
        EventId = 7103,
        Level = LogLevel.Information,
        Message = "TMDB {MediaType} changes chunk processed: start={WindowStart} end={WindowEnd} changedIds={ChangedIds} existingUserRelevant={ExistingUserRelevant} discoveryRelevant={DiscoveryRelevant} discoveryOnlyRelevant={DiscoveryOnlyRelevant} unionRelevant={UnionRelevant} refreshed={Refreshed} skipped={Skipped} failed={Failed}")]
    internal static partial void LogChunkProcessed(
        ILogger logger,
        string mediaType,
        DateOnly windowStart,
        DateOnly windowEnd,
        int changedIds,
        int existingUserRelevant,
        int discoveryRelevant,
        int discoveryOnlyRelevant,
        int unionRelevant,
        int refreshed,
        int skipped,
        int failed);

    [LoggerMessage(
        EventId = 7104,
        Level = LogLevel.Information,
        Message = "TMDB {MediaType} changes run completed: changedIds={ChangedIds} existingUserRelevant={ExistingUserRelevant} discoveryRelevant={DiscoveryRelevant} discoveryOnlyRelevant={DiscoveryOnlyRelevant} unionRelevant={UnionRelevant} refreshed={Refreshed} skipped={Skipped} failed={Failed}")]
    internal static partial void LogRunCompleted(
        ILogger logger,
        string mediaType,
        int changedIds,
        int existingUserRelevant,
        int discoveryRelevant,
        int discoveryOnlyRelevant,
        int unionRelevant,
        int refreshed,
        int skipped,
        int failed);
}
