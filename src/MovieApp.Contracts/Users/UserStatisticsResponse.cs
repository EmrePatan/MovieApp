namespace MovieApp.Contracts.Users;

public sealed record UserStatisticsSummaryResponse(
    int MoviesWatched,
    int EpisodesWatched,
    int ShowsStarted,
    int ShowsCompleted,
    int RatingsCount,
    int ReviewsCount,
    int FavoritesCount,
    int WatchlistCount,
    decimal? AverageStarRating);

public sealed record MonthlyActivityResponse(
    int Year,
    int Month,
    int Movies,
    int Episodes,
    int Total);

public sealed record ActivityMonthHighlightResponse(
    int Year,
    int Month,
    int Movies,
    int Episodes,
    int Total);

public sealed record UserStatisticsActivityResponse(
    IReadOnlyList<MonthlyActivityResponse> Last12Months,
    ActivityMonthHighlightResponse? MostActiveMonth,
    int CurrentMonthTotal,
    int PreviousMonthTotal,
    int? LongestStreakDays);

public sealed record GenreStatisticResponse(
    Guid GenreId,
    string Name,
    int Count);

public sealed record StarRatingDistributionResponse(
    int Stars,
    int Count);

public sealed record UserStatisticsRatingsResponse(
    IReadOnlyList<StarRatingDistributionResponse> Distribution,
    int? MostUsedStars,
    decimal? AverageStarRating);

public sealed record UserStatisticsWatchingMixResponse(
    int MovieTitleCount,
    int SeriesTitleCount);

public sealed record ProfileMilestoneResponse(
    string Id,
    string Title,
    string Description,
    DateTime? AchievedAt);

public sealed record UserStatisticsResponse(
    UserStatisticsSummaryResponse Summary,
    UserStatisticsActivityResponse Activity,
    IReadOnlyList<GenreStatisticResponse> Genres,
    UserStatisticsRatingsResponse Ratings,
    UserStatisticsWatchingMixResponse WatchingMix,
    IReadOnlyList<ProfileMilestoneResponse> Milestones,
    IReadOnlyList<string> Insights);
