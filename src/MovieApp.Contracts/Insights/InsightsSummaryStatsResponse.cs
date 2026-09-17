namespace MovieApp.Contracts.Insights;

public sealed record InsightsSummaryStatsResponse(
    int MoviesWatched,
    int EpisodesWatched,
    int ShowsStarted,
    int RatingsCount,
    decimal? AverageStarRating);
