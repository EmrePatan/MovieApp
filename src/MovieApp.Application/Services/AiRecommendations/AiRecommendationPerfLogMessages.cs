using Microsoft.Extensions.Logging;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Services.AiRecommendations;

internal static partial class AiRecommendationPerfLogMessages
{
    [LoggerMessage(
        EventId = 7201,
        Level = LogLevel.Information,
        Message = "AiRecommendationPerf Outcome={Outcome} TotalMs={TotalMs} QuotaReserveMs={QuotaReserveMs} TasteProfileCache={TasteProfileCache} TasteProfileTotalMs={TasteProfileTotalMs} TasteProfileCacheLookupMs={TasteProfileCacheLookupMs} TasteProfileBuildCpuMs={TasteProfileBuildCpuMs} TasteProfileCacheWriteMs={TasteProfileCacheWriteMs} TasteProfileRatingsMs={TasteProfileRatingsMs} TasteProfileFavoritesMs={TasteProfileFavoritesMs} TasteProfileWatchlistMs={TasteProfileWatchlistMs} TasteProfileWatchedMoviesMs={TasteProfileWatchedMoviesMs} TasteProfileWatchedTvGenresMs={TasteProfileWatchedTvGenresMs} TasteProfileDbRoundTrips={TasteProfileDbRoundTrips} SessionLoadMs={SessionLoadMs} GeminiTotalMs={GeminiTotalMs} GeminiHttpMs={GeminiHttpMs} GeminiParseMs={GeminiParseMs} ValidationMs={ValidationMs} ValidationWatchedIdsMs={ValidationWatchedIdsMs} TmdbResolutionCalls={TmdbResolutionCalls} SessionSaveMs={SessionSaveMs} QuotaCommitMs={QuotaCommitMs} DbRoundTrips={DbRoundTrips} SuggestionCount={SuggestionCount} GeminiSuggestionCount={GeminiSuggestionCount} ReturnedCount={ReturnedCount}")]
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
        int tmdbResolutionCalls,
        long sessionSaveMs,
        long quotaCommitMs,
        int dbRoundTrips,
        int suggestionCount,
        int geminiSuggestionCount,
        int returnedCount);

    internal static void LogRequest(ILogger logger, AiRecommendationPerfMetrics metrics) =>
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
            metrics.TmdbResolutionCalls,
            metrics.SessionSaveMs,
            metrics.QuotaCommitMs,
            metrics.DbRoundTrips,
            metrics.SuggestionCount,
            metrics.GeminiSuggestionCount,
            metrics.ReturnedCount);
}
