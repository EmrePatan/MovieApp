using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class UserRecommendationContextInteractionProbe
{
    internal static async Task<int> CountDistinctMeaningfulInteractionsAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (UsesNpgsql(dbContext))
        {
            return await CountDistinctMeaningfulInteractionsViaSqlAsync(
                dbContext,
                userId,
                cancellationToken);
        }

        return await CountDistinctMeaningfulInteractionsViaEfAsync(
            dbContext,
            userId,
            cancellationToken);
    }

    private static bool UsesNpgsql(ApplicationDbContext dbContext) =>
        dbContext.Database.IsRelational() &&
        dbContext.Database.ProviderName is "Npgsql.EntityFrameworkCore.PostgreSQL";

    private static async Task<int> CountDistinctMeaningfulInteractionsViaSqlAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tvContentType = CatalogContentType.Tv.ToString();

        return await dbContext.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value"
                FROM (
                    SELECT 'movie' AS content_type, r."MovieId" AS content_id
                    FROM ratings AS r
                    WHERE r."UserId" = {userId} AND r."MovieId" IS NOT NULL
                    UNION
                    SELECT 'tv', r."TvShowId"
                    FROM ratings AS r
                    WHERE r."UserId" = {userId} AND r."TvShowId" IS NOT NULL
                    UNION
                    SELECT 'movie', f."MovieId"
                    FROM favorites AS f
                    WHERE f."UserId" = {userId} AND f."MovieId" IS NOT NULL
                    UNION
                    SELECT 'tv', f."TvShowId"
                    FROM favorites AS f
                    WHERE f."UserId" = {userId} AND f."TvShowId" IS NOT NULL
                    UNION
                    SELECT 'movie', wm."MovieId"
                    FROM watched_movies AS wm
                    WHERE wm."UserId" = {userId}
                    UNION
                    SELECT 'movie', wi."MovieId"
                    FROM watchlist_items AS wi
                    INNER JOIN watchlists AS w ON w."Id" = wi."WatchlistId"
                    WHERE w."UserId" = {userId} AND wi."MovieId" IS NOT NULL
                    UNION
                    SELECT 'tv', wi."TvShowId"
                    FROM watchlist_items AS wi
                    INNER JOIN watchlists AS w ON w."Id" = wi."WatchlistId"
                    WHERE w."UserId" = {userId} AND wi."TvShowId" IS NOT NULL
                    UNION
                    SELECT 'tv', s."TvShowId"
                    FROM watched_episodes AS we
                    INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                    INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                    WHERE we."UserId" = {userId}
                    UNION
                    SELECT 'tv', cf."ContentId"
                    FROM catalog_follows AS cf
                    WHERE cf."UserId" = {userId} AND cf."ContentType" = {tvContentType}
                ) AS interactions
                """)
            .SingleAsync(cancellationToken);
    }

    private static async Task<int> CountDistinctMeaningfulInteractionsViaEfAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var ratingRows = await dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.UserId == userId)
            .Select(rating => new UserRecommendationContextModels.RatingRow(
                rating.MovieId,
                rating.TvShowId,
                rating.Score,
                rating.UpdatedAt,
                null,
                null))
            .ToListAsync(cancellationToken);

        var favoriteRows = await dbContext.Favorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .Select(favorite => new UserRecommendationContextModels.TimestampedContentRow(
                favorite.MovieId,
                favorite.TvShowId,
                favorite.CreatedAt,
                null,
                null))
            .ToListAsync(cancellationToken);

        var watchedMovieRows = await dbContext.WatchedMovies
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new UserRecommendationContextModels.WatchedMovieRow(
                item.MovieId,
                string.Empty,
                item.WatchedAt))
            .ToListAsync(cancellationToken);

        var watchlistRows = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.Watchlist.UserId == userId)
            .Select(item => new UserRecommendationContextModels.TimestampedContentRow(
                item.MovieId,
                item.TvShowId,
                item.CreatedAt,
                null,
                null))
            .ToListAsync(cancellationToken);

        var watchedEpisodeRows = await dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new UserRecommendationContextModels.WatchedEpisodeRow(
                item.Episode.Season.TvShowId,
                item.WatchedAt))
            .ToListAsync(cancellationToken);

        var catalogFollowRows = await dbContext.CatalogFollows
            .AsNoTracking()
            .Where(follow => follow.UserId == userId)
            .Select(follow => new UserRecommendationContextModels.CatalogFollowRow(
                follow.ContentType,
                follow.ContentId,
                follow.CreatedAt))
            .ToListAsync(cancellationToken);

        return UserRecommendationContextLoader.CountMeaningfulInteractions(
            ratingRows,
            favoriteRows,
            watchedMovieRows,
            watchlistRows,
            watchedEpisodeRows,
            catalogFollowRows);
    }
}
