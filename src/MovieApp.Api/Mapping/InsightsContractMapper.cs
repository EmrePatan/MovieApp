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

    public static InsightsV3Response ToInsightsV3Response(InsightsV3Result result) =>
        new(
            new InsightsV3MetaResponse(
                result.Meta.MemberSinceUtc,
                result.Meta.GeneratedAtUtc,
                result.Meta.TimeZone,
                result.Meta.Year),
            new InsightsV3MovieDnaResponse(
                result.MovieDna.IdentityTitle,
                result.MovieDna.IdentityCodes,
                result.MovieDna.Labels.Select(ToInsightsMovieDnaLabelResponse).ToList(),
                result.MovieDna.TopGenres.Select(ToInsightsTasteGenreResponse).ToList(),
                new InsightsV3WatchingMixResponse(
                    result.MovieDna.WatchingMix.MovieTitleCount,
                    result.MovieDna.WatchingMix.SeriesTitleCount,
                    result.MovieDna.WatchingMix.MovieSharePercent,
                    result.MovieDna.WatchingMix.SeriesSharePercent)),
            new InsightsV3YourYearResponse(
                result.YourYear.Months.Select(month => new InsightsV3MonthlyActivityResponse(
                    month.Year,
                    month.Month,
                    month.Movies,
                    month.Episodes,
                    month.Total)).ToList(),
                result.YourYear.ActiveDays,
                result.YourYear.PeakMonth is null
                    ? null
                    : new InsightsV3MonthHighlightResponse(
                        result.YourYear.PeakMonth.Year,
                        result.YourYear.PeakMonth.Month,
                        result.YourYear.PeakMonth.Movies,
                        result.YourYear.PeakMonth.Episodes,
                        result.YourYear.PeakMonth.Total),
                result.YourYear.FavoriteWeekday),
            new InsightsV3TasteSectionResponse(
                result.YourTaste.Genres.Select(ToInsightsTasteGenreResponse).ToList(),
                result.YourTaste.RisingGenre is null
                    ? null
                    : new InsightsV3RisingGenreResponse(
                        result.YourTaste.RisingGenre.GenreId,
                        result.YourTaste.RisingGenre.Name,
                        result.YourTaste.RisingGenre.CurrentYearSharePercent,
                        result.YourTaste.RisingGenre.PreviousYearSharePercent,
                        result.YourTaste.RisingGenre.ShareDeltaPercent)),
            new InsightsV3TimeInStoriesResponse(
                result.TimeInStories.TotalMinutes,
                result.TimeInStories.MovieMinutes,
                result.TimeInStories.EpisodeMinutes,
                result.TimeInStories.YearMinutes,
                result.TimeInStories.RuntimeCoveragePercent),
            new InsightsV3RatingsSectionResponse(
                result.YourRatings.Count,
                result.YourRatings.AverageStars,
                result.YourRatings.Distribution
                    .Select(item => new InsightsRatingsDistributionItemResponse(item.Stars, item.Count))
                    .ToList(),
                ToInsightsV3GenreRatingResponse(result.YourRatings.HighestRatedGenre),
                ToInsightsV3GenreRatingResponse(result.YourRatings.LowestRatedGenre)),
            new InsightsV3EraSectionResponse(
                result.YourEra.Decades
                    .Select(bucket => new InsightsEraBucketResponse(bucket.Bucket, bucket.Count, bucket.Percent))
                    .ToList(),
                result.YourEra.FavoriteDecade,
                result.YourEra.UnknownCount,
                result.YourEra.OldestTitle is null
                    ? null
                    : new InsightsV3OldestTitleResponse(
                        result.YourEra.OldestTitle.ContentType,
                        result.YourEra.OldestTitle.ContentId,
                        result.YourEra.OldestTitle.Title,
                        result.YourEra.OldestTitle.Year,
                        result.YourEra.OldestTitle.PosterPath)),
            new InsightsV3RecordsSectionResponse(
                result.YourRecords.LongestStreakDays,
                ToInsightsV3WeeklyPeakResponse(result.YourRecords.BestMovieWeek),
                ToInsightsV3WeeklyPeakResponse(result.YourRecords.BestEpisodeWeek),
                result.YourRecords.HighestRatingStars),
            result.Achievements
                .Select(milestone => new InsightsMilestoneResponse(
                    milestone.Id,
                    milestone.Category,
                    milestone.Title,
                    milestone.CurrentValue,
                    milestone.TargetValue,
                    milestone.Achieved,
                    milestone.AchievedAt))
                .ToList());

    private static InsightsV3GenreRatingResponse? ToInsightsV3GenreRatingResponse(
        InsightsV3GenreRatingResult? genre) =>
        genre is null
            ? null
            : new InsightsV3GenreRatingResponse(
                genre.GenreId,
                genre.Name,
                genre.RatingCount,
                genre.AverageStars);

    private static InsightsV3WeeklyPeakResponse? ToInsightsV3WeeklyPeakResponse(
        InsightsV3WeeklyPeakResult? week) =>
        week is null
            ? null
            : new InsightsV3WeeklyPeakResponse(week.Year, week.Week, week.Count);
}
