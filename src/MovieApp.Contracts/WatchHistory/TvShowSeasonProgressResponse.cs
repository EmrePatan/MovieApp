namespace MovieApp.Contracts.WatchHistory;

public sealed record TvShowSeasonProgressResponse(
    int SeasonNumber,
    int TotalEpisodes,
    int WatchedEpisodes,
    decimal ProgressPercentage);
