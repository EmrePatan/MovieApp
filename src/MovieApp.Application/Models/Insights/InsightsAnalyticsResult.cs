namespace MovieApp.Application.Models.Insights;

public sealed record InsightsActivityDayResult(
    DateOnly Date,
    int Movies,
    int Episodes,
    int Total,
    InsightsActivityDayState State,
    int IntensityBucket);

public sealed record InsightsActivitySummaryResult(
    int TotalActiveDays,
    DayOfWeek? MostActiveWeekday,
    int? LongestStreakDays,
    int CurrentWeekTotal,
    int PreviousWeekTotal);

public sealed record InsightsActivityResult(
    IReadOnlyList<InsightsActivityDayResult> Days,
    InsightsActivitySummaryResult Summary);

public sealed record InsightsTasteGenreResult(
    Guid GenreId,
    string Name,
    decimal Weight,
    decimal SharePercent);

public sealed record InsightsTasteResult(IReadOnlyList<InsightsTasteGenreResult> Genres);

public sealed record InsightsEraBucketResult(
    string Bucket,
    int Count,
    decimal? Percent);

public sealed record InsightsErasResult(
    IReadOnlyList<InsightsEraBucketResult> Buckets,
    int UnknownCount);

public sealed record InsightsEstimatedTimeWatchedResult(
    int TotalEstimatedMinutes,
    int MovieEstimatedMinutes,
    int EpisodeEstimatedMinutes,
    int KnownRuntimeItemCount,
    int TotalWatchedItemCount,
    decimal CoveragePercent,
    int? CurrentYearEstimatedMinutes);

public sealed record InsightsRatingsDistributionItemResult(int Stars, int Count);

public sealed record InsightsRatingsAnalyticsResult(
    int RatingCount,
    decimal? AverageStarRating,
    IReadOnlyList<InsightsRatingsDistributionItemResult> Distribution,
    int? MostUsedStars);

public sealed record InsightsMilestoneResult(
    string Id,
    string Category,
    string Title,
    int CurrentValue,
    int TargetValue,
    bool Achieved,
    DateTime? AchievedAt);

public sealed record InsightsAnalyticsResult(
    InsightsActivityResult Activity,
    InsightsTasteResult Taste,
    InsightsErasResult Eras,
    InsightsEstimatedTimeWatchedResult EstimatedTimeWatched,
    InsightsRatingsAnalyticsResult Ratings,
    IReadOnlyList<InsightsMilestoneResult> Milestones,
    DateTime GeneratedAtUtc);
