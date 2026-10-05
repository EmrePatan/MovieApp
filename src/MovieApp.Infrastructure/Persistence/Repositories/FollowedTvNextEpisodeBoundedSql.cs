using Microsoft.EntityFrameworkCore;
using MovieApp.Infrastructure.Persistence;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class FollowedTvNextEpisodeBoundedSql
{
    internal static IQueryable<CatalogFollowCatalogRepository.FollowedNextEpisodeRow> Query(
        ApplicationDbContext dbContext,
        Guid userId,
        DateOnly today,
        int fetchCount) =>
        dbContext.Database.SqlQuery<CatalogFollowCatalogRepository.FollowedNextEpisodeRow>($"""
            WITH ranked AS (
                SELECT
                    t."Id" AS "ContentId",
                    t."Title",
                    t."PosterPath",
                    e."AirDate" AS "ReleaseDate",
                    e."Id" AS "EpisodeId",
                    s."SeasonNumber",
                    e."EpisodeNumber",
                    e."Name" AS "EpisodeName",
                    ROW_NUMBER() OVER (
                        PARTITION BY t."Id"
                        ORDER BY e."AirDate", s."SeasonNumber", e."EpisodeNumber", e."Id") AS show_row
                FROM catalog_follows AS c
                INNER JOIN tv_shows AS t ON c."ContentId" = t."Id"
                INNER JOIN seasons AS s ON t."Id" = s."TvShowId"
                INNER JOIN episodes AS e ON s."Id" = e."SeasonId"
                WHERE c."UserId" = {userId}
                    AND c."ContentType" = 'Tv'
                    AND e."AirDate" IS NOT NULL
                    AND e."AirDate" > {today}
            )
            SELECT
                "ContentId",
                "Title",
                "PosterPath",
                "ReleaseDate",
                "EpisodeId",
                "SeasonNumber",
                "EpisodeNumber",
                "EpisodeName"
            FROM ranked
            WHERE show_row <= 1
            ORDER BY "ReleaseDate", "ContentId"
            LIMIT {fetchCount}
            """);
}
