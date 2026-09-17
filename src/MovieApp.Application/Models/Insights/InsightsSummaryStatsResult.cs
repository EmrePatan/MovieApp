namespace MovieApp.Application.Models.Insights;

public sealed record InsightsSummaryStatsResult(
    int MoviesWatched,
    int EpisodesWatched,
    int ShowsStarted,
    int RatingsCount,
    decimal? AverageStarRating);
