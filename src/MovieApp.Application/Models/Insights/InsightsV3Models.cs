namespace MovieApp.Application.Models.Insights;

public sealed record InsightsV3MonthlyActivityResult(
    int Year,
    int Month,
    int Movies,
    int Episodes,
    int Total);

public sealed record InsightsV3MonthHighlightResult(
    int Year,
    int Month,
    int Movies,
    int Episodes,
    int Total);

public sealed record InsightsV3YourYearResult(
    IReadOnlyList<InsightsV3MonthlyActivityResult> Months,
    int ActiveDays,
    InsightsV3MonthHighlightResult? PeakMonth,
    DayOfWeek? FavoriteWeekday);

public sealed record InsightsV3WatchingMixResult(
    int MovieTitleCount,
    int SeriesTitleCount,
    decimal MovieSharePercent,
    decimal SeriesSharePercent);

public sealed record InsightsV3MovieDnaResult(
    string IdentityTitle,
    IReadOnlyList<string> IdentityCodes,
    IReadOnlyList<InsightsMovieDnaLabelResult> Labels,
    IReadOnlyList<InsightsTasteGenreResult> TopGenres,
    InsightsV3WatchingMixResult WatchingMix);

public sealed record InsightsV3RisingGenreResult(
    Guid GenreId,
    string Name,
    decimal CurrentYearSharePercent,
    decimal PreviousYearSharePercent,
    decimal ShareDeltaPercent);

public sealed record InsightsV3TasteSectionResult(
    IReadOnlyList<InsightsTasteGenreResult> Genres,
    InsightsV3RisingGenreResult? RisingGenre);

public sealed record InsightsV3TimeInStoriesResult(
    int TotalMinutes,
    int MovieMinutes,
    int EpisodeMinutes,
    int YearMinutes,
    decimal RuntimeCoveragePercent);

public sealed record InsightsV3GenreRatingResult(
    Guid GenreId,
    string Name,
    int RatingCount,
    decimal AverageStars);

public sealed record InsightsV3RatingsSectionResult(
    int Count,
    decimal? AverageStars,
    IReadOnlyList<InsightsRatingsDistributionItemResult> Distribution,
    InsightsV3GenreRatingResult? HighestRatedGenre,
    InsightsV3GenreRatingResult? LowestRatedGenre);

public sealed record InsightsV3OldestTitleResult(
    string ContentType,
    Guid ContentId,
    string Title,
    int? Year,
    string? PosterPath);

public sealed record InsightsV3EraSectionResult(
    IReadOnlyList<InsightsEraBucketResult> Decades,
    string? FavoriteDecade,
    int UnknownCount,
    InsightsV3OldestTitleResult? OldestTitle);

public sealed record InsightsV3WeeklyPeakResult(
    int Year,
    int Week,
    int Count);

public sealed record InsightsV3RecordsRawData(
    int? LongestStreakDays,
    InsightsV3WeeklyPeakResult? BestMovieWeek,
    InsightsV3WeeklyPeakResult? BestEpisodeWeek);

public sealed record InsightsV3RecordsSectionResult(
    int? LongestStreakDays,
    InsightsV3WeeklyPeakResult? BestMovieWeek,
    InsightsV3WeeklyPeakResult? BestEpisodeWeek,
    decimal? HighestRatingStars);

public sealed record InsightsV3MetaResult(
    DateTime MemberSinceUtc,
    DateTime GeneratedAtUtc,
    string TimeZone,
    int Year);

public sealed record InsightsV3Result(
    InsightsV3MetaResult Meta,
    InsightsV3MovieDnaResult MovieDna,
    InsightsV3YourYearResult YourYear,
    InsightsV3TasteSectionResult YourTaste,
    InsightsV3TimeInStoriesResult TimeInStories,
    InsightsV3RatingsSectionResult YourRatings,
    InsightsV3EraSectionResult YourEra,
    InsightsV3RecordsSectionResult YourRecords,
    IReadOnlyList<InsightsMilestoneResult> Achievements);

public sealed record InsightsV3GenreRatingRow(
    Guid GenreId,
    string Name,
    int RatingCount,
    decimal AverageScore);

public sealed record InsightsV3OldestTitleRow(
    string ContentType,
    Guid ContentId,
    string Title,
    int? Year,
    string? PosterPath,
    DateOnly? SortDate);

public sealed record InsightsV3RawData(
    DateTime MemberSinceUtc,
    int DistinctMovieCount,
    int DistinctSeriesCount,
    int MoviesWatched,
    int EpisodesWatched,
    int ShowsStarted,
    int RatingsCount,
    IReadOnlyList<InsightsDnaTitleData> MovieTitles,
    IReadOnlyList<InsightsDnaTitleData> TvShowTitles,
    IReadOnlyList<InsightsDnaTitleData> CurrentYearMovieTitles,
    IReadOnlyList<InsightsDnaTitleData> CurrentYearTvShowTitles,
    IReadOnlyList<InsightsDnaTitleData> PreviousYearMovieTitles,
    IReadOnlyList<InsightsDnaTitleData> PreviousYearTvShowTitles,
    IReadOnlyList<(DateTime WatchedAtUtc, int? RuntimeMinutes)> YearMovieWatches,
    IReadOnlyList<(DateTime WatchedAtUtc, int? RuntimeMinutes)> YearEpisodeWatches,
    InsightsV3RecordsRawData Records,
    int MovieEstimatedMinutes,
    int EpisodeEstimatedMinutes,
    int MoviesWithKnownRuntime,
    int EpisodesWithKnownRuntime,
    IReadOnlyList<(int Score, int Count)> RatingScoreCounts,
    IReadOnlyList<InsightsV3GenreRatingRow> GenreRatings,
    InsightsV3OldestTitleRow? OldestTitle,
    InsightsAnalyticsRawData MilestoneRaw);

public sealed class InsightsV3QueryMetrics
{
    /// <summary>
    /// Logical repository phases executed by <c>GetV3RawDataAsync</c>.
    /// </summary>
    public int DbRoundTrips { get; set; }

    /// <summary>
    /// Actual PostgreSQL commands issued while loading V3 raw data.
    /// </summary>
    public int PgCommandRoundTrips { get; set; }

    public long DbTotalMs { get; set; }

    public long SummaryMs { get; set; }

    public long DnaMs { get; set; }

    public long YearActivityMs { get; set; }

    public long RecordsMs { get; set; }

    public long RuntimeMs { get; set; }

    public long RatingsMs { get; set; }

    public long MilestonesMs { get; set; }
}
