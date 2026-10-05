using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using MovieApp.Application.Abstractions.Caching;
using MovieApp.Application.Models.Home;
using MovieApp.Application.Models.Search;
using MovieApp.Application.Validation;
using MovieApp.Application.Services.Home;
using MovieApp.Application.Services.Localization;
using MovieApp.Infrastructure.Caching;
using MovieApp.Infrastructure.Performance;

namespace MovieApp.HomeStampedeBenchmark;

internal static class Program
{
    private static readonly Guid UserId = Guid.Parse("53fc2a7d-a45d-4037-9d88-43c4757253ac");
    private const string ContentLocale = ContentLocaleResolver.EnglishUnitedStates;

    public static async Task<int> Main()
    {
        await using var provider = BenchmarkHost.CreateServiceProvider(UserId);
        var homeKey = await BenchmarkHost.ResolveHomeCacheKeyAsync(provider, UserId, ContentLocale, releaseRegion: null);
        var recommendationHomeKey = await BenchmarkHost.ResolveRecommendationHomeCacheKeyAsync(provider, UserId, ContentLocale);
        Console.WriteLine($"UserId={UserId}");
        Console.WriteLine($"Home cache key={homeKey}");
        Console.WriteLine($"Recommendation home cache key={recommendationHomeKey}");
        Console.WriteLine($"Locale={ContentLocale} sectionSize={BenchmarkHost.SectionSize}");
        Console.WriteLine();

        await PrimeHomeCachesAsync(provider, homeKey, recommendationHomeKey);

        var lockDiagnostics = provider.GetRequiredService<ISearchRefreshLockDiagnostics>();
        var results = new List<ScenarioResult>();

        results.Add(await RunWarmBaselineAsync(provider, homeKey, lockDiagnostics));
        results.Add(await RunSingleColdAsync(provider, homeKey, recommendationHomeKey, lockDiagnostics));
        results.Add(await RunConcurrentColdAsync(provider, homeKey, recommendationHomeKey, lockDiagnostics, concurrency: 5));
        results.Add(await RunConcurrentColdAsync(provider, homeKey, recommendationHomeKey, lockDiagnostics, concurrency: 10));

        var outputPath = Path.Combine(AppContext.BaseDirectory, "home-stampede-benchmark.json");
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(results, BenchmarkJsonContext.Default.ListScenarioResult));
        Console.WriteLine($"Wrote {outputPath}");
        PrintSummary(results);
        return 0;
    }

    private static async Task<ScenarioResult> RunWarmBaselineAsync(
        ServiceProvider provider,
        string homeKey,
        ISearchRefreshLockDiagnostics lockDiagnostics)
    {
        var lockBefore = SnapshotLockDiagnostics(lockDiagnostics);
        var counters = HomeStampedePerfAmbient.BeginScenario("warm-baseline");
        var request = await ExecuteHomeRequestAsync(provider, "warm-baseline");
        HomeStampedePerfAmbient.EndScenario();
        return ScenarioResult.FromCounters(
            "warm-baseline",
            1,
            [request],
            counters,
            lockDiagnostics,
            lockBefore);
    }

    private static async Task PrimeHomeCachesAsync(
        ServiceProvider provider,
        string homeKey,
        string recommendationHomeKey)
    {
        await InvalidateHomeCachesAsync(provider, homeKey, recommendationHomeKey);
        _ = await ExecuteHomeRequestAsync(provider, "prime");
    }

    private static async Task<ScenarioResult> RunSingleColdAsync(
        ServiceProvider provider,
        string homeKey,
        string recommendationHomeKey,
        ISearchRefreshLockDiagnostics lockDiagnostics)
    {
        await InvalidateHomeCachesAsync(provider, homeKey, recommendationHomeKey);
        var lockBefore = SnapshotLockDiagnostics(lockDiagnostics);
        var counters = HomeStampedePerfAmbient.BeginScenario("single-cold");
        var request = await ExecuteHomeRequestAsync(provider, "single-cold");
        HomeStampedePerfAmbient.EndScenario();
        return ScenarioResult.FromCounters(
            "single-cold",
            1,
            [request],
            counters,
            lockDiagnostics,
            lockBefore);
    }

    private static async Task<ScenarioResult> RunConcurrentColdAsync(
        ServiceProvider provider,
        string homeKey,
        string recommendationHomeKey,
        ISearchRefreshLockDiagnostics lockDiagnostics,
        int concurrency)
    {
        await InvalidateHomeCachesAsync(provider, homeKey, recommendationHomeKey);
        var lockBefore = SnapshotLockDiagnostics(lockDiagnostics);
        var counters = HomeStampedePerfAmbient.BeginScenario($"concurrent-cold-{concurrency}");
        var wallStopwatch = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, concurrency)
            .Select(index => ExecuteHomeRequestAsync(provider, $"cold-{concurrency}-{index}"))
            .ToArray();
        var requests = await Task.WhenAll(tasks);
        wallStopwatch.Stop();
        HomeStampedePerfAmbient.EndScenario();

        var result = ScenarioResult.FromCounters(
            $"concurrent-cold-{concurrency}",
            concurrency,
            requests,
            counters,
            lockDiagnostics,
            lockBefore);
        return result with { WallClockMs = wallStopwatch.ElapsedMilliseconds };
    }

    private static async Task<RequestResult> ExecuteHomeRequestAsync(
        ServiceProvider provider,
        string label)
    {
        using var scope = provider.CreateScope();
        using var perfScope = HomeColdPerfScope.Begin("HomeStampede", label);
        var home = scope.ServiceProvider.GetRequiredService<IHomeService>();
        var stopwatch = Stopwatch.StartNew();
        _ = await home.GetHomeAsync(
            new HomeCriteria(SearchContentType.All, BenchmarkHost.SectionSize),
            ContentLocale);
        stopwatch.Stop();
        var metrics = perfScope.Metrics;
        return new RequestResult(
            label,
            stopwatch.ElapsedMilliseconds,
            metrics.CommandCount,
            metrics.PeakActiveCommands,
            metrics.CommandExecutionMs,
            metrics.ConnectionOpenMs);
    }

    private static void PrintSummary(IReadOnlyList<ScenarioResult> results)
    {
        Console.WriteLine();
        foreach (var scenario in results)
        {
            Console.WriteLine($"=== {scenario.Scenario} (concurrency={scenario.Concurrency}) ===");
            Console.WriteLine(
                $"homeMiss={scenario.HomeCacheMisses} homeBuild={scenario.HomeBuildCompletions} " +
                $"recInvoke={scenario.RecommendationHomeInvocations} recBuild={scenario.RecommendationHomeBuilds} " +
                $"recHit={scenario.RecommendationHomeCacheHits} candidateFetch={scenario.PersonalizedCandidateFetches}");
            Console.WriteLine(
                $"weekly={scenario.WeeklyTrendingBuilds} comingUp={scenario.ComingUpBuilds} " +
                $"onTv={scenario.OnTvBuilds} nowPlaying={scenario.NowInTheatersBuilds}");
            Console.WriteLine(
                $"dbCommands={scenario.TotalDbCommands} peakDb={scenario.PeakDbCommandsAcrossRequests} " +
                $"lockRedisOk+={scenario.LockRedisSuccessDelta} lockRedisContend+={scenario.LockRedisContentionDelta}");
            Console.WriteLine(
                $"latenciesMs=[{string.Join(", ", scenario.Requests.Select(request => request.LatencyMs))}] wallMs={scenario.WallClockMs}");
            Console.WriteLine();
        }
    }

    private static async Task InvalidateHomeCachesAsync(
        ServiceProvider provider,
        string homeKey,
        string recommendationHomeKey)
    {
        using var scope = provider.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cache.RemoveAsync(homeKey);
        await cache.RemoveAsync(recommendationHomeKey);
    }

    private static LockDiagnosticsSnapshot SnapshotLockDiagnostics(ISearchRefreshLockDiagnostics diagnostics) =>
        new(
            diagnostics.RedisAcquireSuccess,
            diagnostics.RedisAcquireContention,
            diagnostics.LocalAcquireSuccess,
            diagnostics.LocalAcquireContention);

    private static LockDiagnosticsSnapshot Delta(
        LockDiagnosticsSnapshot after,
        LockDiagnosticsSnapshot before) =>
        new(
            after.RedisAcquireSuccess - before.RedisAcquireSuccess,
            after.RedisAcquireContention - before.RedisAcquireContention,
            after.LocalAcquireSuccess - before.LocalAcquireSuccess,
            after.LocalAcquireContention - before.LocalAcquireContention);

    internal sealed record RequestResult(
        string Label,
        long LatencyMs,
        int DbCommandCount,
        int PeakActiveDbCommands,
        long DbCommandExecutionMs,
        long DbConnectionOpenMs);

    internal sealed record LockDiagnosticsSnapshot(
        long RedisAcquireSuccess,
        long RedisAcquireContention,
        long LocalAcquireSuccess,
        long LocalAcquireContention);

    internal sealed record ScenarioResult(
        string Scenario,
        int Concurrency,
        long WallClockMs,
        IReadOnlyList<RequestResult> Requests,
        int HomeCacheHits,
        int HomeCacheMisses,
        int HomeBuildCompletions,
        int RecommendationHomeInvocations,
        int RecommendationHomeCacheHits,
        int RecommendationHomeBuilds,
        int PersonalizedCandidateFetches,
        int WeeklyTrendingBuilds,
        int ComingUpBuilds,
        int OnTvBuilds,
        int NowInTheatersBuilds,
        int TotalDbCommands,
        int PeakDbCommandsAcrossRequests,
        long TotalDbCommandExecutionMs,
        long LockRedisSuccessDelta,
        long LockRedisContentionDelta,
        long LockLocalSuccessDelta,
        long LockLocalContentionDelta)
    {
        public static ScenarioResult FromCounters(
            string scenario,
            int concurrency,
            IReadOnlyList<RequestResult> requests,
            HomeStampedePerfCounters counters,
            ISearchRefreshLockDiagnostics lockDiagnostics,
            LockDiagnosticsSnapshot lockBefore)
        {
            var lockAfter = new LockDiagnosticsSnapshot(
                lockDiagnostics.RedisAcquireSuccess,
                lockDiagnostics.RedisAcquireContention,
                lockDiagnostics.LocalAcquireSuccess,
                lockDiagnostics.LocalAcquireContention);
            var lockDelta = Delta(lockAfter, lockBefore);

            return new ScenarioResult(
                scenario,
                concurrency,
                requests.Max(request => request.LatencyMs),
                requests,
                counters.HomeCacheHits,
                counters.HomeCacheMisses,
                counters.HomeBuildCompletions,
                counters.RecommendationHomeInvocations,
                counters.RecommendationHomeCacheHits,
                counters.RecommendationHomeBuilds,
                counters.PersonalizedCandidateFetches,
                counters.WeeklyTrendingBuilds,
                counters.ComingUpBuilds,
                counters.OnTvBuilds,
                counters.NowInTheatersBuilds,
                requests.Sum(request => request.DbCommandCount),
                requests.Max(request => request.PeakActiveDbCommands),
                requests.Sum(request => request.DbCommandExecutionMs),
                lockDelta.RedisAcquireSuccess,
                lockDelta.RedisAcquireContention,
                lockDelta.LocalAcquireSuccess,
                lockDelta.LocalAcquireContention);
        }
    }
}

[JsonSerializable(typeof(List<Program.ScenarioResult>))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class BenchmarkJsonContext : JsonSerializerContext;
