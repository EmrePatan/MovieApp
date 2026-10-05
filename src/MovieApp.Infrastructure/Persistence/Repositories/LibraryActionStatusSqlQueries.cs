using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class LibraryActionStatusSqlQueries
{
    internal sealed class MovieStatusRow
    {
        public bool IsFavorited { get; init; }

        public Guid[] WatchlistIds { get; init; } = [];

        public bool IsFollowing { get; init; }

        public DateTime? WatchedAt { get; init; }
    }

    internal sealed class TvStatusRow
    {
        public bool IsFavorited { get; init; }

        public Guid[] WatchlistIds { get; init; } = [];

        public bool IsFollowing { get; init; }

        public bool? NotifyNewSeasons { get; init; }

        public bool? NotifyNewEpisodes { get; init; }

        public DateTime? BaselineEstablishedAtUtc { get; init; }

        public DateTime? WatchedAt { get; init; }
    }

    internal static async Task<MovieStatusRow> GetMovieStatusAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        Guid movieId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQuery<MovieStatusRow>($"""
                SELECT
                    EXISTS (
                        SELECT 1
                        FROM favorites AS f
                        WHERE f."UserId" = {userId}
                          AND f."MovieId" = {movieId}
                    ) AS "IsFavorited",
                    COALESCE((
                        SELECT array_agg(DISTINCT wi."WatchlistId")
                        FROM watchlist_items AS wi
                        INNER JOIN watchlists AS w ON w."Id" = wi."WatchlistId"
                        WHERE w."UserId" = {userId}
                          AND wi."MovieId" = {movieId}
                    ), ARRAY[]::uuid[]) AS "WatchlistIds",
                    EXISTS (
                        SELECT 1
                        FROM catalog_follows AS cf
                        WHERE cf."UserId" = {userId}
                          AND cf."ContentType" = 'Movie'
                          AND cf."ContentId" = {movieId}
                    ) AS "IsFollowing",
                    (
                        SELECT wm."WatchedAt"
                        FROM watched_movies AS wm
                        WHERE wm."UserId" = {userId}
                          AND wm."MovieId" = {movieId}
                        LIMIT 1
                    ) AS "WatchedAt"
                """)
            .ToListAsync(cancellationToken);

        return rows.Single();
    }

    internal static async Task<TvStatusRow> GetTvShowStatusAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        Guid tvShowId,
        Guid? episodeId,
        CancellationToken cancellationToken)
    {
        return episodeId is Guid episode
            ? await GetTvShowStatusWithEpisodeAsync(
                dbContext,
                userId,
                tvShowId,
                episode,
                cancellationToken)
            : await GetTvShowStatusWithoutEpisodeAsync(
                dbContext,
                userId,
                tvShowId,
                cancellationToken);
    }

    private static async Task<TvStatusRow> GetTvShowStatusWithoutEpisodeAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        Guid tvShowId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQuery<TvStatusRow>($"""
                SELECT
                    EXISTS (
                        SELECT 1
                        FROM favorites AS f
                        WHERE f."UserId" = {userId}
                          AND f."TvShowId" = {tvShowId}
                    ) AS "IsFavorited",
                    COALESCE((
                        SELECT array_agg(DISTINCT wi."WatchlistId")
                        FROM watchlist_items AS wi
                        INNER JOIN watchlists AS w ON w."Id" = wi."WatchlistId"
                        WHERE w."UserId" = {userId}
                          AND wi."TvShowId" = {tvShowId}
                    ), ARRAY[]::uuid[]) AS "WatchlistIds",
                    EXISTS (
                        SELECT 1
                        FROM catalog_follows AS cf
                        WHERE cf."UserId" = {userId}
                          AND cf."ContentType" = 'Tv'
                          AND cf."ContentId" = {tvShowId}
                    ) AS "IsFollowing",
                    (
                        SELECT cf."NotifyNewSeasons"
                        FROM catalog_follows AS cf
                        WHERE cf."UserId" = {userId}
                          AND cf."ContentType" = 'Tv'
                          AND cf."ContentId" = {tvShowId}
                        LIMIT 1
                    ) AS "NotifyNewSeasons",
                    (
                        SELECT cf."NotifyNewEpisodes"
                        FROM catalog_follows AS cf
                        WHERE cf."UserId" = {userId}
                          AND cf."ContentType" = 'Tv'
                          AND cf."ContentId" = {tvShowId}
                        LIMIT 1
                    ) AS "NotifyNewEpisodes",
                    (
                        SELECT cf."BaselineEstablishedAtUtc"
                        FROM catalog_follows AS cf
                        WHERE cf."UserId" = {userId}
                          AND cf."ContentType" = 'Tv'
                          AND cf."ContentId" = {tvShowId}
                        LIMIT 1
                    ) AS "BaselineEstablishedAtUtc",
                    NULL::timestamp with time zone AS "WatchedAt"
                """)
            .ToListAsync(cancellationToken);

        return rows.Single();
    }

    private static async Task<TvStatusRow> GetTvShowStatusWithEpisodeAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        Guid tvShowId,
        Guid episodeId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQuery<TvStatusRow>($"""
                SELECT
                    EXISTS (
                        SELECT 1
                        FROM favorites AS f
                        WHERE f."UserId" = {userId}
                          AND f."TvShowId" = {tvShowId}
                    ) AS "IsFavorited",
                    COALESCE((
                        SELECT array_agg(DISTINCT wi."WatchlistId")
                        FROM watchlist_items AS wi
                        INNER JOIN watchlists AS w ON w."Id" = wi."WatchlistId"
                        WHERE w."UserId" = {userId}
                          AND wi."TvShowId" = {tvShowId}
                    ), ARRAY[]::uuid[]) AS "WatchlistIds",
                    EXISTS (
                        SELECT 1
                        FROM catalog_follows AS cf
                        WHERE cf."UserId" = {userId}
                          AND cf."ContentType" = 'Tv'
                          AND cf."ContentId" = {tvShowId}
                    ) AS "IsFollowing",
                    (
                        SELECT cf."NotifyNewSeasons"
                        FROM catalog_follows AS cf
                        WHERE cf."UserId" = {userId}
                          AND cf."ContentType" = 'Tv'
                          AND cf."ContentId" = {tvShowId}
                        LIMIT 1
                    ) AS "NotifyNewSeasons",
                    (
                        SELECT cf."NotifyNewEpisodes"
                        FROM catalog_follows AS cf
                        WHERE cf."UserId" = {userId}
                          AND cf."ContentType" = 'Tv'
                          AND cf."ContentId" = {tvShowId}
                        LIMIT 1
                    ) AS "NotifyNewEpisodes",
                    (
                        SELECT cf."BaselineEstablishedAtUtc"
                        FROM catalog_follows AS cf
                        WHERE cf."UserId" = {userId}
                          AND cf."ContentType" = 'Tv'
                          AND cf."ContentId" = {tvShowId}
                        LIMIT 1
                    ) AS "BaselineEstablishedAtUtc",
                    (
                        SELECT we."WatchedAt"
                        FROM watched_episodes AS we
                        WHERE we."UserId" = {userId}
                          AND we."EpisodeId" = {episodeId}
                          AND EXISTS (
                              SELECT 1
                              FROM episodes AS e
                              INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                              WHERE e."Id" = we."EpisodeId"
                                AND s."TvShowId" = {tvShowId}
                          )
                        LIMIT 1
                    ) AS "WatchedAt"
                """)
            .ToListAsync(cancellationToken);

        return rows.Single();
    }
}
