using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Abstractions.Persistence;
using MovieApp.Application.Models.Identity;

namespace MovieApp.Infrastructure.Persistence.Repositories;

public sealed class UserStatisticsRepository(ApplicationDbContext dbContext) : IUserStatisticsRepository
{
    public async Task<UserStatisticsResult> GetStatisticsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var favoriteMovieCount = await dbContext.Favorites
            .AsNoTracking()
            .CountAsync(favorite => favorite.UserId == userId && favorite.MovieId != null, cancellationToken);

        var favoriteTvShowCount = await dbContext.Favorites
            .AsNoTracking()
            .CountAsync(favorite => favorite.UserId == userId && favorite.TvShowId != null, cancellationToken);

        var watchlistCount = await dbContext.Watchlists
            .AsNoTracking()
            .CountAsync(watchlist => watchlist.UserId == userId, cancellationToken);

        var watchlistItemCount = await dbContext.WatchlistItems
            .AsNoTracking()
            .CountAsync(item => item.Watchlist.UserId == userId, cancellationToken);

        var ratedMovieCount = await dbContext.Ratings
            .AsNoTracking()
            .CountAsync(rating => rating.UserId == userId && rating.MovieId != null, cancellationToken);

        var ratedTvShowCount = await dbContext.Ratings
            .AsNoTracking()
            .CountAsync(rating => rating.UserId == userId && rating.TvShowId != null, cancellationToken);

        var reviewedMovieCount = await dbContext.Reviews
            .AsNoTracking()
            .CountAsync(review => review.UserId == userId && review.MovieId != null, cancellationToken);

        var reviewedTvShowCount = await dbContext.Reviews
            .AsNoTracking()
            .CountAsync(review => review.UserId == userId && review.TvShowId != null, cancellationToken);

        var watchedMovieCount = await dbContext.WatchedMovies
            .AsNoTracking()
            .CountAsync(watchedMovie => watchedMovie.UserId == userId, cancellationToken);

        var watchedEpisodeCount = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .CountAsync(watchedEpisode => watchedEpisode.UserId == userId, cancellationToken);

        var totalRatingCount = ratedMovieCount + ratedTvShowCount;
        var totalReviewCount = reviewedMovieCount + reviewedTvShowCount;
        var totalWatchedCount = watchedMovieCount + watchedEpisodeCount;

        return new UserStatisticsResult(
            favoriteMovieCount,
            favoriteTvShowCount,
            watchlistCount,
            watchlistItemCount,
            ratedMovieCount,
            ratedTvShowCount,
            reviewedMovieCount,
            reviewedTvShowCount,
            watchedMovieCount,
            watchedEpisodeCount,
            totalRatingCount,
            totalReviewCount,
            totalWatchedCount);
    }
}
