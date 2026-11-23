using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.WatchHistory;

namespace MovieApp.Application.Services.Insights;

/// <summary>
/// Tiered achievements: every <i>family</i> (movies, episodes, streak, ...) is a ladder of tiers
/// (1 = bronze ... 5 = legend) that share one running value. Ids of the original eleven
/// milestones are kept so already-celebrated unlocks are not announced twice.
/// </summary>
public static class InsightsMilestonesBuilder
{
    private const int ClassicCutoffYear = 1980;

    private sealed record Inputs(
        InsightsAnalyticsRawData Raw,
        int DistinctDecades,
        int Classics,
        int LongestStreakDays,
        int Hours,
        int TenureDays);

    private sealed record Family(
        string Category,
        int[] Targets,
        Func<Inputs, int> CurrentValue,
        Func<int, string> Id,
        Func<int, string> Title,
        Func<InsightsAnalyticsRawData, int, DateTime?> AchievedAt);

    private static DateTime? NoDate(InsightsAnalyticsRawData raw, int target) => null;

    private static readonly Family[] Families =
    [
        new("movies", [1, 10, 50, 100, 250],
            inputs => inputs.Raw.MoviesWatched,
            target => target switch { 1 => "first-movie", _ => $"movies-{target}" },
            target => target == 1 ? "First movie watched" : $"{target} movies watched",
            (raw, target) => target switch
            {
                1 => raw.FirstMovieWatchedAtUtc,
                10 => raw.TenthMovieWatchedAtUtc,
                50 => raw.FiftiethMovieWatchedAtUtc,
                _ => null,
            }),
        new("episodes", [25, 100, 500, 1000, 2500],
            inputs => inputs.Raw.EpisodesWatched,
            target => $"episodes-{target}",
            target => $"{target} episodes watched",
            (raw, target) => target switch
            {
                100 => raw.HundredthEpisodeWatchedAtUtc,
                500 => raw.FiveHundredthEpisodeWatchedAtUtc,
                _ => null,
            }),
        new("ratings", [10, 25, 50, 100, 250],
            inputs => inputs.Raw.RatingsCount,
            target => $"ratings-{target}",
            target => $"{target} ratings",
            (raw, target) => target switch
            {
                10 => raw.TenthRatingAtUtc,
                25 => raw.TwentyFifthRatingAtUtc,
                50 => raw.FiftiethRatingAtUtc,
                _ => null,
            }),
        new("shows", [1, 3, 10, 25],
            inputs => CountCompletedShows(inputs.Raw),
            target => target switch
            {
                1 => "first-show-completed",
                3 => "shows-completed-3",
                _ => $"shows-completed-{target}",
            },
            target => target == 1 ? "First series completed" : $"{target} series completed",
            (raw, target) => target switch
            {
                1 => GetFirstCompletedShowAt(raw),
                3 => GetNthCompletedShowAt(raw, 3),
                _ => null,
            }),
        new("genres", [3, 5, 8, 12],
            inputs => CountDistinctGenres(inputs.Raw),
            target => $"genres-{target}",
            target => $"{target} genres encountered",
            NoDate),
        new("eras", [3, 5, 8],
            inputs => inputs.DistinctDecades,
            target => $"eras-{target}",
            target => $"{target} decades explored",
            NoDate),
        new("classics", [3, 10, 25],
            inputs => inputs.Classics,
            target => $"classics-{target}",
            target => $"{target} classics watched",
            NoDate),
        new("streak", [3, 7, 14, 30],
            inputs => inputs.LongestStreakDays,
            target => $"streak-{target}",
            target => $"{target}-day streak",
            NoDate),
        new("hours", [24, 100, 500, 1000],
            inputs => inputs.Hours,
            target => $"hours-{target}",
            target => $"{target} hours watched",
            NoDate),
        new("tenure", [30, 180, 365],
            inputs => inputs.TenureDays,
            target => $"tenure-{target}",
            target => $"{target} days with Movie Cave",
            NoDate),
    ];

    public static IReadOnlyList<InsightsMilestoneResult> Build(InsightsV3RawData raw, DateTime utcNow) =>
        Build(new Inputs(
            raw.MilestoneRaw with { DistinctGenreCount = raw.DistinctGenreCount },
            CountDistinctDecades(raw),
            CountClassics(raw),
            raw.Records.LongestStreakDays ?? 0,
            (raw.MovieEstimatedMinutes + raw.EpisodeEstimatedMinutes) / 60,
            Math.Max(0, (int)(utcNow - raw.MemberSinceUtc).TotalDays)));

    /// <summary>Count-based families only (no release-year, streak or runtime aggregates).</summary>
    public static IReadOnlyList<InsightsMilestoneResult> Build(InsightsAnalyticsRawData raw) =>
        Build(new Inputs(
            raw,
            0,
            0,
            0,
            (raw.MovieEstimatedMinutes + raw.EpisodeEstimatedMinutes) / 60,
            Math.Max(0, (int)(DateTime.UtcNow - raw.MemberSinceUtc).TotalDays)));

    private static List<InsightsMilestoneResult> Build(Inputs inputs)
    {
        var results = new List<InsightsMilestoneResult>();

        foreach (var family in Families)
        {
            var currentValue = family.CurrentValue(inputs);
            for (var index = 0; index < family.Targets.Length; index++)
            {
                var target = family.Targets[index];
                var achieved = currentValue >= target;
                results.Add(new InsightsMilestoneResult(
                    family.Id(target),
                    family.Category,
                    family.Title(target),
                    currentValue,
                    target,
                    achieved,
                    achieved ? family.AchievedAt(inputs.Raw, target) : null,
                    index + 1));
            }
        }

        return results;
    }

    public static int CountCompletedShows(InsightsAnalyticsRawData raw) =>
        raw.ShowCompletions.Count(IsCompleted);

    public static DateTime? GetFirstCompletedShowAt(InsightsAnalyticsRawData raw) =>
        GetCompletedShows(raw)
            .OrderBy(show => show.LastWatchedAtUtc)
            .FirstOrDefault()?.LastWatchedAtUtc;

    public static DateTime? GetNthCompletedShowAt(InsightsAnalyticsRawData raw, int target)
    {
        var completed = GetCompletedShows(raw)
            .OrderBy(show => show.LastWatchedAtUtc)
            .ToList();

        return completed.Count >= target
            ? completed[target - 1].LastWatchedAtUtc
            : null;
    }

    public static int CountDistinctGenres(InsightsAnalyticsRawData raw)
    {
        if (raw.DistinctGenreCount is int precomputed)
        {
            return precomputed;
        }

        return raw.MovieTitles
            .SelectMany(title => title.Genres.Select(genre => genre.GenreId))
            .Concat(raw.TvShowTitles.SelectMany(title => title.Genres.Select(genre => genre.GenreId)))
            .Distinct()
            .Count();
    }

    public static int CountDistinctDecades(InsightsV3RawData raw) =>
        raw.AllTimeReleaseYears
            .Where(item => item.Year is > 1800 && item.Count > 0)
            .Select(item => item.Year!.Value / 10)
            .Distinct()
            .Count();

    public static int CountClassics(InsightsV3RawData raw) =>
        raw.AllTimeReleaseYears
            .Where(item => item.Year is { } year && year < ClassicCutoffYear)
            .Sum(item => item.Count);

    private static IEnumerable<InsightsShowCompletionData> GetCompletedShows(InsightsAnalyticsRawData raw) =>
        raw.ShowCompletions.Where(show => IsCompleted(show) && show.LastWatchedAtUtc is not null);

    private static bool IsCompleted(InsightsShowCompletionData show) =>
        TvShowCompletionPolicy.IsCompleted(show.IsConcluded, show.TotalEpisodes, show.WatchedEpisodes);
}
