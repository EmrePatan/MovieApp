using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Catalog;

internal static partial class CatalogMetadataFreshnessLogMessages
{
    [LoggerMessage(
        EventId = 7200,
        Level = LogLevel.Information,
        Message = "Catalog metadata freshness safety-net completed: eligibleStale={EligibleStale} selected={Selected} moviesSelected={MoviesSelected} tvSelected={TvSelected} refreshed={Refreshed} skipped={Skipped} failed={Failed} remainingEstimate={RemainingEstimate} durationMs={DurationMs}")]
    internal static partial void LogSafetyNetCompleted(
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
        EventId = 7201,
        Level = LogLevel.Warning,
        Message = "Catalog metadata freshness safety-net failed for {MediaType} catalog id {CatalogId}.")]
    internal static partial void LogSafetyNetItemFailed(
        ILogger logger,
        Exception exception,
        string mediaType,
        Guid catalogId);
}
