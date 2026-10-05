using MovieApp.Application.Models.Localization;

namespace MovieApp.Application.Services.Localization;

/// <summary>
/// Optional per-logical-request localization diagnostics. When no ambient scope is active,
/// instrumentation is a no-op and production semantics are unchanged.
/// </summary>
public static class LocalizationOverlayPerfAmbient
{
    private static readonly AsyncLocal<LocalizationOverlayPerfMetrics?> CurrentScope = new();

    public static LocalizationOverlayPerfMetrics? Current => CurrentScope.Value;

    public static LocalizationOverlayPerfMetrics BeginScope(string operationName, string contentLocale)
    {
        var metrics = new LocalizationOverlayPerfMetrics
        {
            OperationName = operationName,
            ContentLocale = contentLocale
        };
        CurrentScope.Value = metrics;
        return metrics;
    }

    public static void EndScope() => CurrentScope.Value = null;

    public static void AddSummaryOverlayTotalMs(long milliseconds)
    {
        var current = Current;
        if (current is null)
        {
            return;
        }

        current.SummaryOverlayTotalMs += milliseconds;
    }

    public static void RecordCatalogGenreEnrichMs(long milliseconds)
    {
        var current = Current;
        if (current is null)
        {
            return;
        }

        current.CatalogGenreEnrichMs = milliseconds;
    }

    public static void RecordMovieProductionContextsMs(long milliseconds)
    {
        var current = Current;
        if (current is null)
        {
            return;
        }

        current.MovieProductionContextsMs = milliseconds;
    }

    public static void RecordTvProductionContextsMs(long milliseconds)
    {
        var current = Current;
        if (current is null)
        {
            return;
        }

        current.TvProductionContextsMs = milliseconds;
    }

    public static void RecordLocalizedMovieTitlesMs(long milliseconds)
    {
        var current = Current;
        if (current is null)
        {
            return;
        }

        current.LocalizedMovieTitlesMs = milliseconds;
    }

    public static void RecordLocalizedTvTitlesMs(long milliseconds)
    {
        var current = Current;
        if (current is null)
        {
            return;
        }

        current.LocalizedTvTitlesMs = milliseconds;
    }

    public static void RecordLocalizedPostersMs(long milliseconds)
    {
        var current = Current;
        if (current is null)
        {
            return;
        }

        current.LocalizedPostersMs = milliseconds;
    }

    public static void RecordPerItemLocalizationPhaseMs(long milliseconds)
    {
        var current = Current;
        if (current is null)
        {
            return;
        }

        current.PerItemLocalizationPhaseMs += milliseconds;
    }

    public static void AddItemCounts(int total, int movies, int tv)
    {
        var current = Current;
        if (current is null)
        {
            return;
        }

        current.ItemCount += total;
        current.MovieItemCount += movies;
        current.TvItemCount += tv;
    }

    public static void RecordOverlayCacheHit()
    {
        Current?.IncrementOverlayCacheHits();
    }

    public static void RecordOverlayCacheMiss()
    {
        Current?.IncrementOverlayCacheMisses();
    }

    public static void RecordDetailKeywordCacheHit() =>
        Current?.IncrementDetailKeywordCacheHits();

    public static void RecordDetailKeywordCacheMiss() =>
        Current?.IncrementDetailKeywordCacheMisses();

    public static void RecordTmdbProviderCall(long milliseconds) =>
        Current?.RecordTmdbProviderCall(milliseconds);
}
