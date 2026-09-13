namespace MovieApp.Application.Models.Identity;

public sealed record UserStatisticsSummaryResult(
    int MoviesWatched,
    int EpisodesWatched,
    int ShowsStarted,
    int ShowsCompleted,
    int RatingsCount,
    int ReviewsCount,
    int FavoritesCount,
    int WatchlistCount,
    decimal? AverageStarRating);

public sealed record MonthlyActivityResult(
    int Year,
    int Month,
    int Movies,
    int Episodes,
    int Total);

public sealed record ActivityMonthHighlightResult(
    int Year,
    int Month,
    int Movies,
    int Episodes,
    int Total);

public sealed record UserStatisticsActivityResult(
    IReadOnlyList<MonthlyActivityResult> Last12Months,
    ActivityMonthHighlightResult? MostActiveMonth,
    int CurrentMonthTotal,
    int PreviousMonthTotal,
    int? LongestStreakDays);

public sealed record GenreStatisticResult(
    Guid GenreId,
    string Name,
    int Count);

public sealed record StarRatingDistributionResult(
    int Stars,
    int Count);

public sealed record UserStatisticsRatingsResult(
    IReadOnlyList<StarRatingDistributionResult> Distribution,
    int? MostUsedStars,
    decimal? AverageStarRating);

public sealed record UserStatisticsWatchingMixResult(
    int MovieTitleCount,
    int SeriesTitleCount);

public sealed record ProfileMilestoneResult(
    string Id,
    string Title,
    string Description,
    DateTime? AchievedAt);

public sealed record UserStatisticsResult(
    UserStatisticsSummaryResult Summary,
    UserStatisticsActivityResult Activity,
    IReadOnlyList<GenreStatisticResult> Genres,
    UserStatisticsRatingsResult Ratings,
    UserStatisticsWatchingMixResult WatchingMix,
    IReadOnlyList<ProfileMilestoneResult> Milestones,
    IReadOnlyList<string> Insights);

public sealed record ProfileStatisticsRawData(
    int FavoriteMovieCount,
    int FavoriteTvShowCount,
    int WatchlistCount,
    int WatchlistItemCount,
    int RatedMovieCount,
    int RatedTvShowCount,
    int ReviewedMovieCount,
    int ReviewedTvShowCount,
    int WatchedMovieCount,
    int WatchedEpisodeCount,
    int ShowsStarted,
    int ShowsCompleted,
    IReadOnlyList<MonthlyActivityResult> MonthlyActivity,
    IReadOnlyList<DateOnly> DistinctWatchDates,
    IReadOnlyList<GenreStatisticResult> Genres,
    IReadOnlyList<(int Score, int Count)> RatingScoreCounts,
    DateTime? FirstMovieWatchedAt,
    DateTime? FirstCompletedShowAt);
