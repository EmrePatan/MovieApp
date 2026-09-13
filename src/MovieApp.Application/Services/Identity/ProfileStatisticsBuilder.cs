using System.Globalization;
using MovieApp.Application.Models.Identity;

namespace MovieApp.Application.Services.Identity;

public static class ProfileStatisticsBuilder
{
    private static readonly (int Threshold, string Id, string Title, string Description)[] MovieMilestones =
    [
        (1, "first-movie", "First movie watched", "You started your movie journey."),
        (10, "movies-10", "10 movies watched", "A solid start to your catalog."),
        (50, "movies-50", "50 movies watched", "You are building a serious watch history."),
    ];

    private static readonly (int Threshold, string Id, string Title, string Description)[] EpisodeMilestones =
    [
        (100, "episodes-100", "100 episodes watched", "Your series habit is real."),
        (500, "episodes-500", "500 episodes watched", "A major binge milestone."),
    ];

    private static readonly (int Threshold, string Id, string Title, string Description)[] RatingMilestones =
    [
        (10, "ratings-10", "10 ratings", "You are shaping your taste profile."),
        (50, "ratings-50", "50 ratings", "Your rating voice is well established."),
    ];

    public static UserStatisticsResult Build(ProfileStatisticsRawData raw, DateTime utcNow)
    {
        var summary = BuildSummary(raw);
        var activity = BuildActivity(raw, utcNow);
        var ratings = BuildRatings(raw);
        var watchingMix = new UserStatisticsWatchingMixResult(
            raw.WatchedMovieCount,
            raw.ShowsStarted);
        var milestones = BuildMilestones(raw);
        var insights = BuildInsights(summary, activity, ratings, watchingMix, raw.Genres);

        return new UserStatisticsResult(
            summary,
            activity,
            raw.Genres,
            ratings,
            watchingMix,
            milestones,
            insights);
    }

    private static UserStatisticsSummaryResult BuildSummary(ProfileStatisticsRawData raw)
    {
        var ratingsCount = raw.RatedMovieCount + raw.RatedTvShowCount;
        var reviewsCount = raw.ReviewedMovieCount + raw.ReviewedTvShowCount;
        var favoritesCount = raw.FavoriteMovieCount + raw.FavoriteTvShowCount;
        var average = CalculateAverageStarRating(raw.RatingScoreCounts);

        return new UserStatisticsSummaryResult(
            raw.WatchedMovieCount,
            raw.WatchedEpisodeCount,
            raw.ShowsStarted,
            raw.ShowsCompleted,
            ratingsCount,
            reviewsCount,
            favoritesCount,
            raw.WatchlistCount,
            average);
    }

    private static UserStatisticsActivityResult BuildActivity(
        ProfileStatisticsRawData raw,
        DateTime utcNow)
    {
        var last12Months = BuildLast12Months(raw.MonthlyActivity, utcNow);
        var mostActive = last12Months
            .Where(month => month.Total > 0)
            .OrderByDescending(month => month.Total)
            .ThenByDescending(month => month.Year)
            .ThenByDescending(month => month.Month)
            .Select(month => new ActivityMonthHighlightResult(
                month.Year,
                month.Month,
                month.Movies,
                month.Episodes,
                month.Total))
            .FirstOrDefault();

        var currentMonth = new DateOnly(utcNow.Year, utcNow.Month, 1);
        var previousMonth = currentMonth.AddMonths(-1);
        var currentMonthTotal = last12Months
            .FirstOrDefault(month => month.Year == currentMonth.Year && month.Month == currentMonth.Month)?.Total ?? 0;
        var previousMonthTotal = last12Months
            .FirstOrDefault(month => month.Year == previousMonth.Year && month.Month == previousMonth.Month)?.Total ?? 0;

        return new UserStatisticsActivityResult(
            last12Months,
            mostActive,
            currentMonthTotal,
            previousMonthTotal,
            CalculateLongestStreakDays(raw.DistinctWatchDates));
    }

    public static IReadOnlyList<MonthlyActivityResult> BuildLast12Months(
        IReadOnlyList<MonthlyActivityResult> monthlyActivity,
        DateTime utcNow)
    {
        var currentMonth = new DateOnly(utcNow.Year, utcNow.Month, 1);
        var months = new List<MonthlyActivityResult>(12);

        for (var offset = 11; offset >= 0; offset--)
        {
            var monthStart = currentMonth.AddMonths(-offset);
            var existing = monthlyActivity.FirstOrDefault(
                month => month.Year == monthStart.Year && month.Month == monthStart.Month);

            months.Add(existing ?? new MonthlyActivityResult(
                monthStart.Year,
                monthStart.Month,
                0,
                0,
                0));
        }

        return months;
    }

    public static int? CalculateLongestStreakDays(IReadOnlyList<DateOnly> distinctWatchDates)
    {
        if (distinctWatchDates.Count == 0)
        {
            return null;
        }

        var orderedDates = distinctWatchDates.OrderBy(date => date).ToList();
        var longest = 1;
        var current = 1;

        for (var index = 1; index < orderedDates.Count; index++)
        {
            if (orderedDates[index].AddDays(-1) == orderedDates[index - 1])
            {
                current++;
                longest = Math.Max(longest, current);
                continue;
            }

            current = 1;
        }

        return longest;
    }

    private static UserStatisticsRatingsResult BuildRatings(ProfileStatisticsRawData raw)
    {
        var distribution = BuildStarDistribution(raw.RatingScoreCounts);
        var mostUsed = distribution
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.Stars)
            .FirstOrDefault(item => item.Count > 0)?.Stars;

        return new UserStatisticsRatingsResult(
            distribution,
            mostUsed,
            CalculateAverageStarRating(raw.RatingScoreCounts));
    }

    public static IReadOnlyList<StarRatingDistributionResult> BuildStarDistribution(
        IReadOnlyList<(int Score, int Count)> ratingScoreCounts)
    {
        var buckets = Enumerable.Range(1, 5)
            .Select(stars => new StarRatingDistributionResult(stars, 0))
            .ToDictionary(item => item.Stars);

        foreach (var (score, count) in ratingScoreCounts)
        {
            var stars = ScoreToWholeStars(score);
            buckets[stars] = new StarRatingDistributionResult(stars, buckets[stars].Count + count);
        }

        return buckets.Values.OrderByDescending(item => item.Stars).ToList();
    }

    public static int ScoreToWholeStars(int score) =>
        Math.Clamp((int)Math.Ceiling(score / 2d), 1, 5);

    public static decimal? CalculateAverageStarRating(IReadOnlyList<(int Score, int Count)> ratingScoreCounts)
    {
        var totalCount = ratingScoreCounts.Sum(item => item.Count);
        if (totalCount == 0)
        {
            return null;
        }

        var weightedScore = ratingScoreCounts.Sum(item => item.Score * item.Count);
        return Math.Round(weightedScore / (decimal)totalCount / 2m, 1, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<ProfileMilestoneResult> BuildMilestones(ProfileStatisticsRawData raw)
    {
        var milestones = new List<ProfileMilestoneResult>();

        foreach (var milestone in MovieMilestones)
        {
            if (raw.WatchedMovieCount >= milestone.Threshold)
            {
                milestones.Add(new ProfileMilestoneResult(
                    milestone.Id,
                    milestone.Title,
                    milestone.Description,
                    raw.FirstMovieWatchedAt));
            }
        }

        foreach (var milestone in EpisodeMilestones)
        {
            if (raw.WatchedEpisodeCount >= milestone.Threshold)
            {
                milestones.Add(new ProfileMilestoneResult(
                    milestone.Id,
                    milestone.Title,
                    milestone.Description,
                    null));
            }
        }

        var ratingsCount = raw.RatedMovieCount + raw.RatedTvShowCount;
        foreach (var milestone in RatingMilestones)
        {
            if (ratingsCount >= milestone.Threshold)
            {
                milestones.Add(new ProfileMilestoneResult(
                    milestone.Id,
                    milestone.Title,
                    milestone.Description,
                    null));
            }
        }

        if (raw.ShowsCompleted >= 1)
        {
            milestones.Add(new ProfileMilestoneResult(
                "first-show-completed",
                "First series completed",
                "You finished every episode of a show.",
                raw.FirstCompletedShowAt));
        }

        return milestones;
    }

    public static IReadOnlyList<string> BuildInsights(
        UserStatisticsSummaryResult summary,
        UserStatisticsActivityResult activity,
        UserStatisticsRatingsResult ratings,
        UserStatisticsWatchingMixResult watchingMix,
        IReadOnlyList<GenreStatisticResult> genres)
    {
        var insights = new List<string>();
        var totalTitles = watchingMix.MovieTitleCount + watchingMix.SeriesTitleCount;

        if (genres.Count > 0 && totalTitles >= 3)
        {
            var topGenres = genres
                .OrderByDescending(genre => genre.Count)
                .Take(2)
                .ToList();
            var genreTotal = genres.Sum(genre => genre.Count);
            var topShare = topGenres[0].Count / (decimal)genreTotal;

            if (topShare >= 0.15m)
            {
                if (topGenres.Count > 1 && topGenres[1].Count / (decimal)genreTotal >= 0.15m)
                {
                    insights.Add($"{topGenres[0].Name} and {topGenres[1].Name} are your top genres.");
                }
                else
                {
                    insights.Add($"{topGenres[0].Name} is your top genre.");
                }
            }
        }

        if (activity.MostActiveMonth is not null && activity.MostActiveMonth.Total >= 3)
        {
            insights.Add($"You watched most in {FormatMonthName(activity.MostActiveMonth.Month)}.");
        }

        if (ratings.MostUsedStars is int mostUsedStars && summary.RatingsCount >= 5)
        {
            insights.Add($"{mostUsedStars}★ is your most-used rating.");
        }

        if (totalTitles >= 4)
        {
            var seriesShare = Math.Round(
                watchingMix.SeriesTitleCount * 100m / totalTitles,
                0,
                MidpointRounding.AwayFromZero);
            if (seriesShare >= 60)
            {
                insights.Add($"Series make up {seriesShare}% of your unique titles.");
            }
            else if (seriesShare <= 40)
            {
                insights.Add($"Movies make up {100 - seriesShare}% of your unique titles.");
            }
        }

        return insights.Take(3).ToList();
    }

    public static string FormatMonthName(int month) =>
        new DateOnly(2026, month, 1).ToString("MMMM", CultureInfo.InvariantCulture);
}
