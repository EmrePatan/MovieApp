using Microsoft.Extensions.Logging;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Services.AiRecommendations;

internal static partial class AiRecommendationPerfLogMessages
{
    [LoggerMessage(
        EventId = 7201,
        Level = LogLevel.Debug,
        Message = "AiRecommendationPerf Outcome={Outcome} TotalMs={TotalMs} QuotaReserveMs={QuotaReserveMs} TasteProfileCache={TasteProfileCache} TasteProfileTotalMs={TasteProfileTotalMs} TasteProfileCacheLookupMs={TasteProfileCacheLookupMs} TasteProfileBuildCpuMs={TasteProfileBuildCpuMs} TasteProfileCacheWriteMs={TasteProfileCacheWriteMs} TasteProfileRatingsMs={TasteProfileRatingsMs} TasteProfileFavoritesMs={TasteProfileFavoritesMs} TasteProfileWatchlistMs={TasteProfileWatchlistMs} TasteProfileWatchedMoviesMs={TasteProfileWatchedMoviesMs} TasteProfileWatchedTvGenresMs={TasteProfileWatchedTvGenresMs} TasteProfileDbRoundTrips={TasteProfileDbRoundTrips} SessionLoadMs={SessionLoadMs} GeminiTotalMs={GeminiTotalMs} GeminiHttpMs={GeminiHttpMs} GeminiParseMs={GeminiParseMs} ValidationMs={ValidationMs} ValidationWatchedIdsMs={ValidationWatchedIdsMs} ValidationResolutionMs={ValidationResolutionMs} ValidationCatalogHits={ValidationCatalogHits} ValidationProviderFallbacks={ValidationProviderFallbacks} ValidationSearchFallbacks={ValidationSearchFallbacks} ValidationDedupHits={ValidationDedupHits} ValidationRejectedUnsupportedMediaType={ValidationRejectedUnsupportedMediaType} ValidationRejectedResolutionFailure={ValidationRejectedResolutionFailure} ValidationRejectedResponseDuplicate={ValidationRejectedResponseDuplicate} ValidationRejectedSessionDuplicate={ValidationRejectedSessionDuplicate} ValidationRejectedWatched={ValidationRejectedWatched} ValidationRejectedExcludedGenre={ValidationRejectedExcludedGenre} ValidationRejectedRuntime={ValidationRejectedRuntime} ValidationRejectedYear={ValidationRejectedYear} TmdbResolutionCalls={TmdbResolutionCalls} SessionSaveMs={SessionSaveMs} QuotaCommitMs={QuotaCommitMs} DbRoundTrips={DbRoundTrips} SuggestionCount={SuggestionCount} GeminiSuggestionCount={GeminiSuggestionCount} ReturnedCount={ReturnedCount}")]
    public static partial void LogRequest(
        ILogger logger,
        string outcome,
        long totalMs,
        long quotaReserveMs,
        string tasteProfileCache,
        long tasteProfileTotalMs,
        long tasteProfileCacheLookupMs,
        long tasteProfileBuildCpuMs,
        long tasteProfileCacheWriteMs,
        long tasteProfileRatingsMs,
        long tasteProfileFavoritesMs,
        long tasteProfileWatchlistMs,
        long tasteProfileWatchedMoviesMs,
        long tasteProfileWatchedTvGenresMs,
        int tasteProfileDbRoundTrips,
        long sessionLoadMs,
        long geminiTotalMs,
        long geminiHttpMs,
        long geminiParseMs,
        long validationMs,
        long validationWatchedIdsMs,
        long validationResolutionMs,
        int validationCatalogHits,
        int validationProviderFallbacks,
        int validationSearchFallbacks,
        int validationDedupHits,
        int validationRejectedUnsupportedMediaType,
        int validationRejectedResolutionFailure,
        int validationRejectedResponseDuplicate,
        int validationRejectedSessionDuplicate,
        int validationRejectedWatched,
        int validationRejectedExcludedGenre,
        int validationRejectedRuntime,
        int validationRejectedYear,
        int tmdbResolutionCalls,
        long sessionSaveMs,
        long quotaCommitMs,
        int dbRoundTrips,
        int suggestionCount,
        int geminiSuggestionCount,
        int returnedCount);

    [LoggerMessage(
        EventId = 7202,
        Level = LogLevel.Debug,
        Message = "AiRecommendationProviderChain SuccessfulProvider={SuccessfulProvider} ProviderAttemptCount={ProviderAttemptCount} ProviderSkippedNotConfiguredCount={ProviderSkippedNotConfiguredCount} LlmChainMs={LlmChainMs} DeterministicFallbackUsed={DeterministicFallbackUsed} DeterministicMs={DeterministicMs} LlmChainBudgetExhausted={LlmChainBudgetExhausted} ProviderFailureSummary={ProviderFailureSummary}")]
    public static partial void LogProviderChain(
        ILogger logger,
        string successfulProvider,
        int providerAttemptCount,
        int providerSkippedNotConfiguredCount,
        long llmChainMs,
        bool deterministicFallbackUsed,
        long deterministicMs,
        bool llmChainBudgetExhausted,
        string providerFailureSummary);

    internal static void LogRequest(ILogger logger, AiRecommendationPerfMetrics metrics)
    {
        LogProviderChain(
            logger,
            metrics.SuccessfulProvider,
            metrics.ProviderAttemptCount,
            metrics.ProviderSkippedNotConfiguredCount,
            metrics.LlmChainMs,
            metrics.DeterministicFallbackUsed,
            metrics.DeterministicMs,
            metrics.LlmChainBudgetExhausted,
            metrics.ProviderFailureSummary);

        LogRequest(
            logger,
            metrics.Outcome,
            metrics.TotalMs,
            metrics.QuotaReserveMs,
            metrics.TasteProfileCache,
            metrics.TasteProfileTotalMs,
            metrics.TasteProfileCacheLookupMs,
            metrics.TasteProfileBuildCpuMs,
            metrics.TasteProfileCacheWriteMs,
            metrics.TasteProfileRatingsMs,
            metrics.TasteProfileFavoritesMs,
            metrics.TasteProfileWatchlistMs,
            metrics.TasteProfileWatchedMoviesMs,
            metrics.TasteProfileWatchedTvGenresMs,
            metrics.TasteProfileDbRoundTrips,
            metrics.SessionLoadMs,
            metrics.GeminiTotalMs,
            metrics.GeminiHttpMs,
            metrics.GeminiParseMs,
            metrics.ValidationMs,
            metrics.ValidationWatchedIdsMs,
            metrics.ValidationResolutionMs,
            metrics.ValidationCatalogHits,
            metrics.ValidationProviderFallbacks,
            metrics.ValidationSearchFallbacks,
            metrics.ValidationDedupHits,
            metrics.ValidationRejectedUnsupportedMediaType,
            metrics.ValidationRejectedResolutionFailure,
            metrics.ValidationRejectedResponseDuplicate,
            metrics.ValidationRejectedSessionDuplicate,
            metrics.ValidationRejectedWatched,
            metrics.ValidationRejectedExcludedGenre,
            metrics.ValidationRejectedRuntime,
            metrics.ValidationRejectedYear,
            metrics.TmdbResolutionCalls,
            metrics.SessionSaveMs,
            metrics.QuotaCommitMs,
            metrics.DbRoundTrips,
            metrics.SuggestionCount,
            metrics.GeminiSuggestionCount,
            metrics.ReturnedCount);
    }
}
