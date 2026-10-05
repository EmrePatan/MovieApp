namespace MovieApp.Application.Models.Localization;

public sealed class LocalizationOverlayPerfMetrics
{
    private long _tmdbMaxLatencyMs;

    public string? OperationName { get; set; }

    public string? ContentLocale { get; set; }

    public long TotalRequestMs { get; set; }

    public long SummaryOverlayTotalMs { get; set; }

    public long CatalogGenreEnrichMs { get; set; }

    public long MovieProductionContextsMs { get; set; }

    public long TvProductionContextsMs { get; set; }

    public long LocalizedMovieTitlesMs { get; set; }

    public long LocalizedTvTitlesMs { get; set; }

    public long LocalizedPostersMs { get; set; }

    public long PerItemLocalizationPhaseMs { get; set; }

    public int ItemCount { get; set; }

    public int MovieItemCount { get; set; }

    public int TvItemCount { get; set; }

    private int _overlayCacheHits;
    private int _overlayCacheMisses;
    private int _detailKeywordCacheHits;
    private int _detailKeywordCacheMisses;
    private int _tmdbProviderCalls;
    private long _tmdbProviderTotalMs;

    public int OverlayCacheHits => _overlayCacheHits;

    public int OverlayCacheMisses => _overlayCacheMisses;

    public int DetailKeywordCacheHits => _detailKeywordCacheHits;

    public int DetailKeywordCacheMisses => _detailKeywordCacheMisses;

    public int TmdbProviderCalls => _tmdbProviderCalls;

    public long TmdbProviderTotalMs => _tmdbProviderTotalMs;

    public void IncrementOverlayCacheHits() => Interlocked.Increment(ref _overlayCacheHits);

    public void IncrementOverlayCacheMisses() => Interlocked.Increment(ref _overlayCacheMisses);

    public void IncrementDetailKeywordCacheHits() => Interlocked.Increment(ref _detailKeywordCacheHits);

    public void IncrementDetailKeywordCacheMisses() => Interlocked.Increment(ref _detailKeywordCacheMisses);

    public void RecordTmdbProviderCall(long milliseconds)
    {
        Interlocked.Increment(ref _tmdbProviderCalls);
        Interlocked.Add(ref _tmdbProviderTotalMs, milliseconds);
        RecordTmdbLatency(milliseconds);
    }

    public long TmdbProviderMaxMs
    {
        get => _tmdbMaxLatencyMs;
        set => _tmdbMaxLatencyMs = value;
    }

    public void RecordTmdbLatency(long milliseconds)
    {
        var observed = milliseconds;
        while (true)
        {
            var currentMax = _tmdbMaxLatencyMs;
            if (observed <= currentMax)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _tmdbMaxLatencyMs, observed, currentMax) == currentMax)
            {
                return;
            }
        }
    }
}
