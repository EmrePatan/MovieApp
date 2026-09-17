namespace MovieApp.Application.Models.Insights;

public sealed record InsightsSummaryRawData(
    DateTime MemberSince,
    int MoviesWatched,
    int EpisodesWatched,
    int ShowsStarted,
    int RatingsCount,
    IReadOnlyList<(int Score, int Count)> RatingScoreCounts,
    IReadOnlyList<InsightsDnaTitleData> MovieTitles,
    IReadOnlyList<InsightsDnaTitleData> TvShowTitles);
