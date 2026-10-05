namespace MovieApp.Application.Services.Home;

/// <summary>
/// Opt-in Home stampede diagnostics. No-op when <see cref="Current"/> is null.
/// </summary>
public static class HomeStampedePerfAmbient
{
    private static readonly AsyncLocal<HomeStampedePerfCounters?> Active = new();

    public static HomeStampedePerfCounters? Current => Active.Value;

    public static HomeStampedePerfCounters BeginScenario(string scenarioName)
    {
        var counters = new HomeStampedePerfCounters(scenarioName);
        Active.Value = counters;
        return counters;
    }

    public static void EndScenario() => Active.Value = null;

    public static void RecordHomeCacheHit() => Current?.IncrementHomeCacheHits();

    public static void RecordHomeCacheMiss() => Current?.IncrementHomeCacheMisses();

    public static void RecordHomeBuildCompleted() => Current?.IncrementHomeBuildCompletions();

    public static void RecordRecommendationHomeCacheHit() => Current?.IncrementRecommendationHomeCacheHits();

    public static void RecordRecommendationHomeInvocation() =>
        Current?.IncrementRecommendationHomeInvocations();

    public static void RecordRecommendationHomeBuildStarted() =>
        Current?.IncrementRecommendationHomeBuilds();

    public static void RecordPersonalizedCandidateFetch() =>
        Current?.IncrementPersonalizedCandidateFetches();

    public static void RecordWeeklyTrendingBuild() => Current?.IncrementWeeklyTrendingBuilds();

    public static void RecordComingUpBuild() => Current?.IncrementComingUpBuilds();

    public static void RecordOnTvBuild() => Current?.IncrementOnTvBuilds();

    public static void RecordNowInTheatersBuild() => Current?.IncrementNowInTheatersBuilds();
}

public sealed class HomeStampedePerfCounters(string scenarioName)
{
    private int _homeCacheHits;
    private int _homeCacheMisses;
    private int _homeBuildCompletions;
    private int _recommendationHomeInvocations;
    private int _recommendationHomeCacheHits;
    private int _recommendationHomeBuilds;
    private int _personalizedCandidateFetches;
    private int _weeklyTrendingBuilds;
    private int _comingUpBuilds;
    private int _onTvBuilds;
    private int _nowInTheatersBuilds;

    public string ScenarioName { get; } = scenarioName;

    public int HomeCacheHits => _homeCacheHits;

    public int HomeCacheMisses => _homeCacheMisses;

    public int HomeBuildCompletions => _homeBuildCompletions;

    public int RecommendationHomeInvocations => _recommendationHomeInvocations;

    public int RecommendationHomeCacheHits => _recommendationHomeCacheHits;

    public int RecommendationHomeBuilds => _recommendationHomeBuilds;

    public int PersonalizedCandidateFetches => _personalizedCandidateFetches;

    public int WeeklyTrendingBuilds => _weeklyTrendingBuilds;

    public int ComingUpBuilds => _comingUpBuilds;

    public int OnTvBuilds => _onTvBuilds;

    public int NowInTheatersBuilds => _nowInTheatersBuilds;

    public void IncrementHomeCacheHits() => Interlocked.Increment(ref _homeCacheHits);

    public void IncrementHomeCacheMisses() => Interlocked.Increment(ref _homeCacheMisses);

    public void IncrementHomeBuildCompletions() => Interlocked.Increment(ref _homeBuildCompletions);

    public void IncrementRecommendationHomeInvocations() => Interlocked.Increment(ref _recommendationHomeInvocations);

    public void IncrementRecommendationHomeCacheHits() => Interlocked.Increment(ref _recommendationHomeCacheHits);

    public void IncrementRecommendationHomeBuilds() => Interlocked.Increment(ref _recommendationHomeBuilds);

    public void IncrementPersonalizedCandidateFetches() => Interlocked.Increment(ref _personalizedCandidateFetches);

    public void IncrementWeeklyTrendingBuilds() => Interlocked.Increment(ref _weeklyTrendingBuilds);

    public void IncrementComingUpBuilds() => Interlocked.Increment(ref _comingUpBuilds);

    public void IncrementOnTvBuilds() => Interlocked.Increment(ref _onTvBuilds);

    public void IncrementNowInTheatersBuilds() => Interlocked.Increment(ref _nowInTheatersBuilds);
}
