using MovieApp.Application.Models.Insights;
using MovieApp.Contracts.Insights;

namespace MovieApp.Api.Mapping;

public static class InsightsContractMapper
{
    public static InsightsSummaryResponse ToInsightsSummaryResponse(InsightsSummaryResult result) =>
        new(
            result.MemberSince,
            result.MovieDna.Select(ToInsightsMovieDnaLabelResponse).ToList(),
            new InsightsSummaryStatsResponse(
                result.Summary.MoviesWatched,
                result.Summary.EpisodesWatched,
                result.Summary.ShowsStarted,
                result.Summary.RatingsCount,
                result.Summary.AverageStarRating),
            new InsightsWatchingMixResponse(
                result.WatchingMix.MovieTitleCount,
                result.WatchingMix.SeriesTitleCount),
            result.GeneratedAtUtc);

    private static InsightsMovieDnaLabelResponse ToInsightsMovieDnaLabelResponse(
        InsightsMovieDnaLabelResult result) =>
        new(result.Code, result.Category, result.Label);

    public static InsightsAnalyticsResponse ToInsightsAnalyticsResponse(InsightsAnalyticsResult result) =>
        new(
            new InsightsActivityResponse(
                result.Activity.Days.Select(ToInsightsActivityDayResponse).ToList(),
                new InsightsActivitySummaryResponse(
                    result.Activity.Summary.TotalActiveDays,
                    result.Activity.Summary.MostActiveWeekday,
                    result.Activity.Summary.LongestStreakDays,
                    result.Activity.Summary.CurrentWeekTotal,
                    result.Activity.Summary.PreviousWeekTotal)),
            new InsightsTasteResponse(
                result.Taste.Genres.Select(ToInsightsTasteGenreResponse).ToList()),
            new InsightsErasResponse(
                result.Eras.Buckets.Select(bucket => new InsightsEraBucketResponse(
                    bucket.Bucket,
                    bucket.Count,
                    bucket.Percent)).ToList(),
                result.Eras.UnknownCount),
            new InsightsEstimatedTimeWatchedResponse(
                result.EstimatedTimeWatched.TotalEstimatedMinutes,
                result.EstimatedTimeWatched.MovieEstimatedMinutes,
                result.EstimatedTimeWatched.EpisodeEstimatedMinutes,
                result.EstimatedTimeWatched.KnownRuntimeItemCount,
                result.EstimatedTimeWatched.TotalWatchedItemCount,
                result.EstimatedTimeWatched.CoveragePercent,
                result.EstimatedTimeWatched.CurrentYearEstimatedMinutes),
            new InsightsRatingsAnalyticsResponse(
                result.Ratings.RatingCount,
                result.Ratings.AverageStarRating,
                result.Ratings.Distribution
                    .Select(item => new InsightsRatingsDistributionItemResponse(item.Stars, item.Count))
                    .ToList(),
                result.Ratings.MostUsedStars),
            result.Milestones
                .Select(milestone => new InsightsMilestoneResponse(
                    milestone.Id,
                    milestone.Category,
                    milestone.Title,
                    milestone.CurrentValue,
                    milestone.TargetValue,
                    milestone.Achieved,
                    milestone.AchievedAt))
                .ToList(),
            result.GeneratedAtUtc);

    private static InsightsActivityDayResponse ToInsightsActivityDayResponse(InsightsActivityDayResult day) =>
        new(
            day.Date,
            day.Movies,
            day.Episodes,
            day.Total,
            day.State switch
            {
                InsightsActivityDayState.Active => InsightsActivityDayStateResponse.Active,
                InsightsActivityDayState.NoActivity => InsightsActivityDayStateResponse.NoActivity,
                InsightsActivityDayState.BeforeJoin => InsightsActivityDayStateResponse.BeforeJoin,
                _ => InsightsActivityDayStateResponse.NoActivity
            },
            day.IntensityBucket);

    private static InsightsTasteGenreResponse ToInsightsTasteGenreResponse(InsightsTasteGenreResult genre) =>
        new(genre.GenreId, genre.Name, genre.Weight, genre.SharePercent);
}
