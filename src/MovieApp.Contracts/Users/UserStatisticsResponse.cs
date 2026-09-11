namespace MovieApp.Contracts.Users;

public sealed record UserStatisticsResponse(
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
    int TotalRatingCount,
    int TotalReviewCount,
    int TotalWatchedCount);
