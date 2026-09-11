namespace MovieApp.Application.Models.Identity;

public sealed record UserStatisticsResult(
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
