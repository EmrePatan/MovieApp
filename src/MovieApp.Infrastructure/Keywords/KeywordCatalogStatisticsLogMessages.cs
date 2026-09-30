using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Keywords;

internal static partial class KeywordCatalogStatisticsLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Keyword catalog statistics refresh started.")]
    public static partial void LogRefreshStarted(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Keyword catalog statistics refresh completed. CatalogDocumentCount={CatalogDocumentCount} KeywordCount={KeywordCount} GenericKeywordCount={GenericKeywordCount} DurationMs={DurationMs} FrequencyAwareActive={FrequencyAwareActive}")]
    public static partial void LogRefreshCompleted(
        ILogger logger,
        int catalogDocumentCount,
        int keywordCount,
        int genericKeywordCount,
        long durationMs,
        bool frequencyAwareActive);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Keyword catalog statistics refresh failed. Reason={Reason} DurationMs={DurationMs}")]
    public static partial void LogRefreshFailed(ILogger logger, string reason, long durationMs);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Keyword catalog statistics refresh skipped because feature is disabled.")]
    public static partial void LogRefreshSkippedDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Keyword catalog statistics startup refresh failed; recommendations will use legacy keyword scoring until a later refresh succeeds.")]
    public static partial void LogStartupRefreshFailed(ILogger logger, Exception exception);
}
