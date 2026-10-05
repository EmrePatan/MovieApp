using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Caching;
using MovieApp.Application.Models.Localization;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Application.Services.Search;
using MovieApp.Application.Validation;

namespace MovieApp.LocalizationPerfBenchmark;

internal static class Program
{
    private const int SectionSize = 20;

    public static async Task<int> Main()
    {
        await using var provider = BenchmarkHost.CreateServiceProvider();
        var results = new List<BenchmarkRunResult>();

        await WarmAsync(provider, iterations: 4);
        await ClearTurkishDetailOverlayCacheForTrendingAsync(provider);

        results.Add(await MeasureExploreAsync(provider, ContentLocaleResolver.EnglishUnitedStates, "en-US-warm", 1));
        results.Add(await MeasureTrendingAsync(provider, ContentLocaleResolver.EnglishUnitedStates, "en-US-warm", 1));

        results.Add(await MeasureExploreAsync(provider, ContentLocaleResolver.TurkishTurkey, "tr-TR-first", 1));
        results.Add(await MeasureTrendingAsync(provider, ContentLocaleResolver.TurkishTurkey, "tr-TR-first", 1));

        results.Add(await MeasureExploreAsync(provider, ContentLocaleResolver.TurkishTurkey, "tr-TR-second", 2));
        results.Add(await MeasureTrendingAsync(provider, ContentLocaleResolver.TurkishTurkey, "tr-TR-second", 2));

        var outputPath = Path.Combine(AppContext.BaseDirectory, "localization-perf-benchmark.json");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(results, BenchmarkJsonContext.Default.ListBenchmarkRunResult));

        Console.WriteLine($"Wrote {outputPath}");
        PrintTable(results);
        return 0;
    }

    private static async Task WarmAsync(ServiceProvider provider, int iterations)
    {
        for (var i = 0; i < iterations; i++)
        {
            _ = await MeasureExploreAsync(provider, ContentLocaleResolver.EnglishUnitedStates, "warmup", 0);
            _ = await MeasureTrendingAsync(provider, ContentLocaleResolver.EnglishUnitedStates, "warmup", 0);
        }
    }

    private static async Task ClearTurkishDetailOverlayCacheForTrendingAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        await InvalidateTrendingResponseCacheAsync(scope.ServiceProvider, ContentLocaleResolver.TurkishTurkey);
        var trending = scope.ServiceProvider.GetRequiredService<ITrendingWeekListService>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var page = await trending.GetPageAsync(
            new DiscoveryCriteria(SearchContentType.All, 1, SectionSize),
            ContentLocaleResolver.TurkishTurkey,
            CancellationToken.None);

        foreach (var item in page.Items)
        {
            if (item.TmdbId is not > 0)
            {
                continue;
            }

            var key = string.Equals(item.Type, "tv", StringComparison.OrdinalIgnoreCase)
                ? DetailLocalizationCacheKeys.TvShow(item.TmdbId.Value, ContentLocaleResolver.TurkishTurkey)
                : DetailLocalizationCacheKeys.Movie(item.TmdbId.Value, ContentLocaleResolver.TurkishTurkey);
            await cache.RemoveAsync(key);
        }
    }

    private static async Task<BenchmarkRunResult> MeasureExploreAsync(
        ServiceProvider provider,
        string contentLocale,
        string label,
        int trPass)
    {
        using var scope = provider.CreateScope();
        await InvalidateExploreResponseCacheAsync(scope.ServiceProvider, contentLocale);
        var explore = scope.ServiceProvider.GetRequiredService<IExplorePreviewService>();
        var metrics = LocalizationOverlayPerfAmbient.BeginScope("GET /api/discovery/explore-preview", contentLocale);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _ = await explore.GetPreviewAsync(
                new ExplorePreviewCriteria(SectionSize),
                contentLocale,
                CancellationToken.None);
        }
        finally
        {
            stopwatch.Stop();
            metrics.TotalRequestMs = stopwatch.ElapsedMilliseconds;
            LocalizationOverlayPerfAmbient.EndScope();
        }

        return BenchmarkRunResult.FromMetrics(label, trPass, metrics);
    }

    private static async Task<BenchmarkRunResult> MeasureTrendingAsync(
        ServiceProvider provider,
        string contentLocale,
        string label,
        int trPass)
    {
        using var scope = provider.CreateScope();
        await InvalidateTrendingResponseCacheAsync(scope.ServiceProvider, contentLocale);
        var trending = scope.ServiceProvider.GetRequiredService<ITrendingWeekListService>();
        var metrics = LocalizationOverlayPerfAmbient.BeginScope("GET /api/discovery/trending", contentLocale);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _ = await trending.GetPageAsync(
                new DiscoveryCriteria(SearchContentType.All, 1, SectionSize),
                contentLocale,
                CancellationToken.None);
        }
        finally
        {
            stopwatch.Stop();
            metrics.TotalRequestMs = stopwatch.ElapsedMilliseconds;
            LocalizationOverlayPerfAmbient.EndScope();
        }

        return BenchmarkRunResult.FromMetrics(label, trPass, metrics);
    }

    private static async Task InvalidateExploreResponseCacheAsync(
        IServiceProvider services,
        string contentLocale)
    {
        var cache = services.GetRequiredService<ICacheService>();
        await cache.RemoveAsync(ExplorePreviewCacheKeys.Create(SectionSize, contentLocale));
    }

    private static async Task InvalidateTrendingResponseCacheAsync(
        IServiceProvider services,
        string contentLocale)
    {
        var cache = services.GetRequiredService<ICacheService>();
        var snapshotService = services.GetRequiredService<IHotThisWeekTrendingSnapshotService>();
        var criteria = new DiscoveryCriteria(SearchContentType.All, 1, SectionSize);
        var snapshot = await snapshotService.GetSnapshotAsync(CancellationToken.None);
        await cache.RemoveAsync(DiscoveryTrendingCacheKeys.CreateWeekList(criteria, contentLocale, snapshot));
        await cache.RemoveAsync(DiscoveryTrendingCacheKeys.Create(criteria, contentLocale));
    }

    private static void PrintTable(IReadOnlyList<BenchmarkRunResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("endpoint | locale | label | totalMs | locTotalMs | perItemMs | items | overlayHit | overlayMiss | tmdbCalls | tmdbTotalMs | tmdbMaxMs");
        foreach (var row in results.Where(r => r.TrPass >= 0))
        {
            Console.WriteLine(
                $"{row.Endpoint} | {row.ContentLocale} | {row.Label} | {row.TotalRequestMs} | {row.SummaryOverlayTotalMs} | {row.PerItemLocalizationPhaseMs} | {row.ItemCount} | {row.OverlayCacheHits} | {row.OverlayCacheMisses} | {row.TmdbProviderCalls} | {row.TmdbProviderTotalMs} | {row.TmdbProviderMaxMs}");
        }
    }

    internal sealed record BenchmarkRunResult(
        string Endpoint,
        string ContentLocale,
        string Label,
        int TrPass,
        long TotalRequestMs,
        long SummaryOverlayTotalMs,
        long CatalogGenreEnrichMs,
        long MovieProductionContextsMs,
        long TvProductionContextsMs,
        long LocalizedMovieTitlesMs,
        long LocalizedTvTitlesMs,
        long LocalizedPostersMs,
        long PerItemLocalizationPhaseMs,
        int ItemCount,
        int MovieItemCount,
        int TvItemCount,
        int OverlayCacheHits,
        int OverlayCacheMisses,
        int TmdbProviderCalls,
        long TmdbProviderTotalMs,
        long TmdbProviderMaxMs)
    {
        public static BenchmarkRunResult FromMetrics(string label, int trPass, LocalizationOverlayPerfMetrics metrics) =>
            new(
                metrics.OperationName ?? "unknown",
                metrics.ContentLocale ?? "unknown",
                label,
                trPass,
                metrics.TotalRequestMs,
                metrics.SummaryOverlayTotalMs,
                metrics.CatalogGenreEnrichMs,
                metrics.MovieProductionContextsMs,
                metrics.TvProductionContextsMs,
                metrics.LocalizedMovieTitlesMs,
                metrics.LocalizedTvTitlesMs,
                metrics.LocalizedPostersMs,
                metrics.PerItemLocalizationPhaseMs,
                metrics.ItemCount,
                metrics.MovieItemCount,
                metrics.TvItemCount,
                metrics.OverlayCacheHits,
                metrics.OverlayCacheMisses,
                metrics.TmdbProviderCalls,
                metrics.TmdbProviderTotalMs,
                metrics.TmdbProviderMaxMs);
    }
}

[JsonSerializable(typeof(List<Program.BenchmarkRunResult>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class BenchmarkJsonContext : JsonSerializerContext;
