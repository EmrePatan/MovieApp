using MovieApp.Application.Models.Insights;
using MovieApp.Application.Services.WatchHistory;

namespace MovieApp.Application.Services.Insights;

public static class InsightsMilestonesBuilder
{
    private static readonly (string Id, string Category, string Title, int Target, Func<InsightsAnalyticsRawData, int> CurrentValue, Func<InsightsAnalyticsRawData, DateTime?> AchievedAt)[] MilestoneDefinitions =
    [
        ("first-movie", "movies", "First movie watched", 1, raw => raw.MoviesWatched, raw => raw.FirstMovieWatchedAtUtc),
        ("movies-10", "movies", "10 movies watched", 10, raw => raw.MoviesWatched, raw => raw.TenthMovieWatchedAtUtc),
        ("movies-50", "movies", "50 movies watched", 50, raw => raw.MoviesWatched, raw => raw.FiftiethMovieWatchedAtUtc),
        ("episodes-100", "episodes", "100 episodes watched", 100, raw => raw.EpisodesWatched, raw => raw.HundredthEpisodeWatchedAtUtc),
        ("episodes-500", "episodes", "500 episodes watched", 500, raw => raw.EpisodesWatched, raw => raw.FiveHundredthEpisodeWatchedAtUtc),
        ("ratings-10", "ratings", "10 ratings", 10, raw => raw.RatingsCount, raw => raw.TenthRatingAtUtc),
        ("ratings-25", "ratings", "25 ratings", 25, raw => raw.RatingsCount, raw => raw.TwentyFifthRatingAtUtc),
        ("ratings-50", "ratings", "50 ratings", 50, raw => raw.RatingsCount, raw => raw.FiftiethRatingAtUtc),
        ("first-show-completed", "shows", "First series completed", 1, raw => CountCompletedShows(raw), raw => GetFirstCompletedShowAt(raw)),
        ("shows-completed-3", "shows", "3 series completed", 3, raw => CountCompletedShows(raw), raw => GetNthCompletedShowAt(raw, 3)),
        ("genres-5", "genres", "5 genres encountered", 5, raw => CountDistinctGenres(raw), _ => null),
    ];

    public static IReadOnlyList<InsightsMilestoneResult> Build(InsightsAnalyticsRawData raw)
    {
        return MilestoneDefinitions
            .Select(definition =>
            {
                var currentValue = definition.CurrentValue(raw);
                var achieved = currentValue >= definition.Target;
                return new InsightsMilestoneResult(
                    definition.Id,
                    definition.Category,
                    definition.Title,
                    currentValue,
                    definition.Target,
                    achieved,
                    achieved ? definition.AchievedAt(raw) : null);
            })
            .ToList();
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
        return raw.MovieTitles
            .SelectMany(title => title.Genres.Select(genre => genre.GenreId))
            .Concat(raw.TvShowTitles.SelectMany(title => title.Genres.Select(genre => genre.GenreId)))
            .Distinct()
            .Count();
    }

    private static IEnumerable<InsightsShowCompletionData> GetCompletedShows(InsightsAnalyticsRawData raw) =>
        raw.ShowCompletions.Where(show => IsCompleted(show) && show.LastWatchedAtUtc is not null);

    private static bool IsCompleted(InsightsShowCompletionData show) =>
        TvShowCompletionPolicy.IsCompleted(show.IsConcluded, show.TotalEpisodes, show.WatchedEpisodes);
}
