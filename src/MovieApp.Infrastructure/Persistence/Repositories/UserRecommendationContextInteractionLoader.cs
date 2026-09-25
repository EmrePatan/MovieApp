using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Enums;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class UserRecommendationContextInteractionLoader
{
    internal sealed record UserInteractionSnapshot(
        IReadOnlyList<UserRecommendationContextModels.RatingRow> RatingRows,
        IReadOnlyList<UserRecommendationContextModels.TimestampedContentRow> FavoriteRows,
        IReadOnlyList<UserRecommendationContextModels.WatchedMovieRow> WatchedMovieRows,
        IReadOnlyList<UserRecommendationContextModels.TimestampedContentRow> WatchlistRows,
        IReadOnlyList<UserRecommendationContextModels.WatchedEpisodeRow> WatchedEpisodeRows,
        IReadOnlyList<UserRecommendationContextModels.CatalogFollowRow> CatalogFollowRows,
        IReadOnlyList<string> RecentQueries,
        long RatingsMs,
        long FavoritesMs,
        long WatchedMoviesMs,
        long WatchlistMs,
        long WatchedEpisodesMs,
        long CatalogFollowsMs,
        long SearchHistoryMs,
        string ExecutionMode,
        int DbRoundTrips);

    internal static Task<UserInteractionSnapshot> LoadAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken) =>
        LoadSequentiallyAsync(dbContext, userId, metrics, cancellationToken);

    private static async Task<UserInteractionSnapshot> LoadSequentiallyAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        var ratings = await TimedLoadAsync(
            () => LoadRatingsAsync(dbContext, userId, metrics, cancellationToken));
        var favorites = await TimedLoadAsync(
            () => LoadFavoritesAsync(dbContext, userId, metrics, cancellationToken));
        var watchedMovies = await TimedLoadAsync(
            () => LoadWatchedMoviesAsync(dbContext, userId, metrics, cancellationToken));
        var watchlist = await TimedLoadAsync(
            () => LoadWatchlistAsync(dbContext, userId, metrics, cancellationToken));
        var watchedEpisodes = await TimedLoadAsync(
            () => LoadWatchedEpisodesAsync(dbContext, userId, metrics, cancellationToken));
        var catalogFollows = await TimedLoadAsync(
            () => LoadCatalogFollowsAsync(dbContext, userId, metrics, cancellationToken));
        var searchHistory = await TimedLoadAsync(
            () => LoadSearchHistoryAsync(dbContext, userId, metrics, cancellationToken));

        return new UserInteractionSnapshot(
            ratings.Result,
            favorites.Result,
            watchedMovies.Result,
            watchlist.Result,
            watchedEpisodes.Result,
            catalogFollows.Result,
            searchHistory.Result,
            ratings.ElapsedMs,
            favorites.ElapsedMs,
            watchedMovies.ElapsedMs,
            watchlist.ElapsedMs,
            watchedEpisodes.ElapsedMs,
            catalogFollows.ElapsedMs,
            searchHistory.ElapsedMs,
            "sequential",
            metrics.DbRoundTrips);
    }

    private static async Task<(T Result, long ElapsedMs)> TimedLoadAsync<T>(Func<Task<T>> load)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await load();
        stopwatch.Stop();
        return (result, stopwatch.ElapsedMilliseconds);
    }

    private static async Task<List<UserRecommendationContextModels.RatingRow>> LoadRatingsAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        metrics.RecordRoundTrip();
        return await dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId)
            .Select(rating => new UserRecommendationContextModels.RatingRow(
                rating.MovieId,
                rating.TvShowId,
                rating.Score,
                rating.UpdatedAt,
                rating.MovieId != null ? rating.Movie!.Title : null,
                rating.TvShowId != null ? rating.TvShow!.Title : null))
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<UserRecommendationContextModels.TimestampedContentRow>> LoadFavoritesAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        metrics.RecordRoundTrip();
        return await dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .Select(favorite => new UserRecommendationContextModels.TimestampedContentRow(
                favorite.MovieId,
                favorite.TvShowId,
                favorite.CreatedAt,
                favorite.MovieId != null ? favorite.Movie!.Title : null,
                favorite.TvShowId != null ? favorite.TvShow!.Title : null))
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<UserRecommendationContextModels.WatchedMovieRow>> LoadWatchedMoviesAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        metrics.RecordRoundTrip();
        return await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.WatchedAt)
            .Select(item => new UserRecommendationContextModels.WatchedMovieRow(item.MovieId, item.Movie!.Title, item.WatchedAt))
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<UserRecommendationContextModels.TimestampedContentRow>> LoadWatchlistAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        metrics.RecordRoundTrip();
        return await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.Watchlist.UserId == userId)
            .Select(item => new UserRecommendationContextModels.TimestampedContentRow(
                item.MovieId,
                item.TvShowId,
                item.CreatedAt,
                item.MovieId != null ? item.Movie!.Title : null,
                item.TvShowId != null ? item.TvShow!.Title : null))
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<UserRecommendationContextModels.WatchedEpisodeRow>> LoadWatchedEpisodesAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        metrics.RecordRoundTrip();
        return await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new UserRecommendationContextModels.WatchedEpisodeRow(
                item.Episode.Season.TvShowId,
                item.WatchedAt))
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<UserRecommendationContextModels.CatalogFollowRow>> LoadCatalogFollowsAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        metrics.RecordRoundTrip();
        return await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.UserId == userId)
            .Select(follow => new UserRecommendationContextModels.CatalogFollowRow(
                follow.ContentType,
                follow.ContentId,
                follow.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<string>> LoadSearchHistoryAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        RecommendationQueryMetrics metrics,
        CancellationToken cancellationToken)
    {
        metrics.RecordRoundTrip();
        return await dbContext.SearchHistories
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.SearchedAt)
            .Select(item => item.NormalizedQuery)
            .Take(UserRecommendationContextModels.MaxSearchQueries)
            .ToListAsync(cancellationToken);
    }
}
