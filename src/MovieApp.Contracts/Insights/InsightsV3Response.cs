namespace MovieApp.Contracts.Insights;

public sealed record InsightsV3MetaResponse(
    DateTime MemberSinceUtc,
    DateTime GeneratedAtUtc,
    string TimeZone,
    int Year);

public sealed record InsightsV3WatchingMixResponse(
    int MovieTitleCount,
    int SeriesTitleCount,
    decimal MovieSharePercent,
    decimal SeriesSharePercent);

public sealed record InsightsV3MovieDnaResponse(
    string IdentityTitle,
    IReadOnlyList<string> IdentityCodes,
    IReadOnlyList<InsightsMovieDnaLabelResponse> Labels,
    IReadOnlyList<InsightsTasteGenreResponse> TopGenres,
    InsightsV3WatchingMixResponse WatchingMix);

public sealed record InsightsV3MonthlyActivityResponse(
    int Year,
    int Month,
    int Movies,
    int Episodes,
    int Total);

public sealed record InsightsV3MonthHighlightResponse(
    int Year,
    int Month,
    int Movies,
    int Episodes,
    int Total);

public sealed record InsightsV3YourYearResponse(
    IReadOnlyList<InsightsV3MonthlyActivityResponse> Months,
    int ActiveDays,
    InsightsV3MonthHighlightResponse? PeakMonth,
    DayOfWeek? FavoriteWeekday);

public sealed record InsightsV3RisingGenreResponse(
    Guid GenreId,
    string Name,
    decimal CurrentYearSharePercent,
    decimal PreviousYearSharePercent,
    decimal ShareDeltaPercent);

public sealed record InsightsV3TasteSectionResponse(
    IReadOnlyList<InsightsTasteGenreResponse> Genres,
    InsightsV3RisingGenreResponse? RisingGenre);

public sealed record InsightsV3TimeInStoriesResponse(
    int TotalMinutes,
    int MovieMinutes,
    int EpisodeMinutes,
    int YearMinutes,
    decimal RuntimeCoveragePercent);

public sealed record InsightsV3GenreRatingResponse(
    Guid GenreId,
    string Name,
    int RatingCount,
    decimal AverageStars);

public sealed record InsightsV3RatingsSectionResponse(
    int Count,
    decimal? AverageStars,
    IReadOnlyList<InsightsRatingsDistributionItemResponse> Distribution,
    InsightsV3GenreRatingResponse? HighestRatedGenre,
    InsightsV3GenreRatingResponse? LowestRatedGenre);

public sealed record InsightsV3OldestTitleResponse(
    string ContentType,
    Guid ContentId,
    string Title,
    int? Year,
    string? PosterPath);

public sealed record InsightsV3EraSectionResponse(
    IReadOnlyList<InsightsEraBucketResponse> Decades,
    string? FavoriteDecade,
    int UnknownCount,
    InsightsV3OldestTitleResponse? OldestTitle);

public sealed record InsightsV3WeeklyPeakResponse(
    int Year,
    int Week,
    int Count);

public sealed record InsightsV3RecordsSectionResponse(
    int? LongestStreakDays,
    InsightsV3WeeklyPeakResponse? BestMovieWeek,
    InsightsV3WeeklyPeakResponse? BestEpisodeWeek,
    decimal? HighestRatingStars);

public sealed record InsightsV3Response(
    InsightsV3MetaResponse Meta,
    InsightsV3MovieDnaResponse MovieDna,
    InsightsV3YourYearResponse YourYear,
    InsightsV3TasteSectionResponse YourTaste,
    InsightsV3TimeInStoriesResponse TimeInStories,
    InsightsV3RatingsSectionResponse YourRatings,
    InsightsV3EraSectionResponse YourEra,
    InsightsV3RecordsSectionResponse YourRecords,
    IReadOnlyList<InsightsMilestoneResponse> Achievements);
