using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Insights;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class InsightsV3SqlQueries
{
    private sealed class RecordsRow
    {
        public int? LongestStreakDays { get; init; }

        public int? BestMovieWeekYear { get; init; }

        public int? BestMovieWeekNumber { get; init; }

        public int? BestMovieWeekCount { get; init; }

        public int? BestEpisodeWeekYear { get; init; }

        public int? BestEpisodeWeekNumber { get; init; }

        public int? BestEpisodeWeekCount { get; init; }
    }

    internal sealed class MilestoneTimestampRow
    {
        public DateTime? FirstMovieWatchedAt { get; init; }

        public DateTime? TenthMovieWatchedAt { get; init; }

        public DateTime? FiftiethMovieWatchedAt { get; init; }

        public DateTime? HundredthEpisodeWatchedAt { get; init; }

        public DateTime? FiveHundredthEpisodeWatchedAt { get; init; }

        public DateTime? TenthRatingAt { get; init; }

        public DateTime? TwentyFifthRatingAt { get; init; }

        public DateTime? FiftiethRatingAt { get; init; }
    }

    internal static async Task<InsightsV3RecordsRawData> GetRecordsAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Database
            .SqlQuery<RecordsRow>($"""
                WITH distinct_dates AS (
                    SELECT DISTINCT (timezone({timeZoneId}, watched_at))::date AS local_date
                    FROM (
                        SELECT wm."WatchedAt" AS watched_at
                        FROM watched_movies AS wm
                        WHERE wm."UserId" = {userId}
                        UNION ALL
                        SELECT we."WatchedAt" AS watched_at
                        FROM watched_episodes AS we
                        WHERE we."UserId" = {userId}
                    ) AS all_watches
                ),
                streak_groups AS (
                    SELECT
                        local_date,
                        local_date - (ROW_NUMBER() OVER (ORDER BY local_date))::int AS grp
                    FROM distinct_dates
                ),
                streak_lengths AS (
                    SELECT COUNT(*)::int AS streak_length
                    FROM streak_groups
                    GROUP BY grp
                ),
                movie_weeks AS (
                    SELECT
                        CAST(to_char(timezone({timeZoneId}, wm."WatchedAt"), 'IYYY') AS integer) AS iso_year,
                        CAST(to_char(timezone({timeZoneId}, wm."WatchedAt"), 'IW') AS integer) AS iso_week,
                        COUNT(*)::integer AS watch_count
                    FROM watched_movies AS wm
                    WHERE wm."UserId" = {userId}
                    GROUP BY 1, 2
                ),
                episode_weeks AS (
                    SELECT
                        CAST(to_char(timezone({timeZoneId}, we."WatchedAt"), 'IYYY') AS integer) AS iso_year,
                        CAST(to_char(timezone({timeZoneId}, we."WatchedAt"), 'IW') AS integer) AS iso_week,
                        COUNT(*)::integer AS watch_count
                    FROM watched_episodes AS we
                    WHERE we."UserId" = {userId}
                    GROUP BY 1, 2
                ),
                best_movie_week AS (
                    SELECT iso_year, iso_week, watch_count
                    FROM movie_weeks
                    ORDER BY watch_count DESC, iso_year DESC, iso_week DESC
                    LIMIT 1
                ),
                best_episode_week AS (
                    SELECT iso_year, iso_week, watch_count
                    FROM episode_weeks
                    ORDER BY watch_count DESC, iso_year DESC, iso_week DESC
                    LIMIT 1
                )
                SELECT
                    (SELECT MAX(streak_length) FROM streak_lengths) AS "LongestStreakDays",
                    (SELECT iso_year FROM best_movie_week) AS "BestMovieWeekYear",
                    (SELECT iso_week FROM best_movie_week) AS "BestMovieWeekNumber",
                    (SELECT watch_count FROM best_movie_week) AS "BestMovieWeekCount",
                    (SELECT iso_year FROM best_episode_week) AS "BestEpisodeWeekYear",
                    (SELECT iso_week FROM best_episode_week) AS "BestEpisodeWeekNumber",
                    (SELECT watch_count FROM best_episode_week) AS "BestEpisodeWeekCount"
                """)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return new InsightsV3RecordsRawData(null, null, null);
        }

        var bestMovieWeek = row.BestMovieWeekCount is > 0
            ? new InsightsV3WeeklyPeakResult(
                row.BestMovieWeekYear ?? 0,
                row.BestMovieWeekNumber ?? 0,
                row.BestMovieWeekCount ?? 0)
            : null;
        var bestEpisodeWeek = row.BestEpisodeWeekCount is > 0
            ? new InsightsV3WeeklyPeakResult(
                row.BestEpisodeWeekYear ?? 0,
                row.BestEpisodeWeekNumber ?? 0,
                row.BestEpisodeWeekCount ?? 0)
            : null;

        return new InsightsV3RecordsRawData(
            row.LongestStreakDays is > 0 ? row.LongestStreakDays : null,
            bestMovieWeek,
            bestEpisodeWeek);
    }

    internal static async Task<MilestoneTimestampRow> GetMilestoneTimestampsAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Database
            .SqlQuery<MilestoneTimestampRow>($"""
                SELECT
                    (SELECT wm."WatchedAt"
                     FROM watched_movies AS wm
                     WHERE wm."UserId" = {userId}
                     ORDER BY wm."WatchedAt"
                     OFFSET 0 LIMIT 1) AS "FirstMovieWatchedAt",
                    (SELECT wm."WatchedAt"
                     FROM watched_movies AS wm
                     WHERE wm."UserId" = {userId}
                     ORDER BY wm."WatchedAt"
                     OFFSET 9 LIMIT 1) AS "TenthMovieWatchedAt",
                    (SELECT wm."WatchedAt"
                     FROM watched_movies AS wm
                     WHERE wm."UserId" = {userId}
                     ORDER BY wm."WatchedAt"
                     OFFSET 49 LIMIT 1) AS "FiftiethMovieWatchedAt",
                    (SELECT we."WatchedAt"
                     FROM watched_episodes AS we
                     WHERE we."UserId" = {userId}
                     ORDER BY we."WatchedAt"
                     OFFSET 99 LIMIT 1) AS "HundredthEpisodeWatchedAt",
                    (SELECT we."WatchedAt"
                     FROM watched_episodes AS we
                     WHERE we."UserId" = {userId}
                     ORDER BY we."WatchedAt"
                     OFFSET 499 LIMIT 1) AS "FiveHundredthEpisodeWatchedAt",
                    (SELECT r."CreatedAt"
                     FROM ratings AS r
                     WHERE r."UserId" = {userId}
                     ORDER BY r."CreatedAt"
                     OFFSET 9 LIMIT 1) AS "TenthRatingAt",
                    (SELECT r."CreatedAt"
                     FROM ratings AS r
                     WHERE r."UserId" = {userId}
                     ORDER BY r."CreatedAt"
                     OFFSET 24 LIMIT 1) AS "TwentyFifthRatingAt",
                    (SELECT r."CreatedAt"
                     FROM ratings AS r
                     WHERE r."UserId" = {userId}
                     ORDER BY r."CreatedAt"
                     OFFSET 49 LIMIT 1) AS "FiftiethRatingAt"
                """)
            .FirstOrDefaultAsync(cancellationToken);

        return row ?? new MilestoneTimestampRow();
    }

    internal sealed class V3RuntimeTotalsSqlRow
    {
        public int MovieTotalMinutes { get; init; }

        public int MovieKnownCount { get; init; }

        public int EpisodeTotalMinutes { get; init; }

        public int EpisodeKnownCount { get; init; }
    }

    internal sealed class V3GenreRatingSqlRow
    {
        public Guid GenreId { get; init; }

        public string Name { get; init; } = string.Empty;

        public int Score { get; init; }
    }

    internal sealed class V3OldestTitleSqlRow
    {
        public string ContentType { get; init; } = string.Empty;

        public Guid ContentId { get; init; }

        public string Title { get; init; } = string.Empty;

        public int? Year { get; init; }

        public string? PosterPath { get; init; }

        public DateOnly? SortDate { get; init; }
    }

    internal static async Task<V3RuntimeTotalsSqlRow> GetRuntimeTotalsAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Database
            .SqlQuery<V3RuntimeTotalsSqlRow>($"""
                SELECT
                    COALESCE((
                        SELECT SUM(m."RuntimeMinutes")
                        FROM watched_movies AS wm
                        INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                        WHERE wm."UserId" = {userId} AND m."RuntimeMinutes" > 0
                    ), 0)::integer AS "MovieTotalMinutes",
                    COALESCE((
                        SELECT COUNT(*)::integer
                        FROM watched_movies AS wm
                        INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                        WHERE wm."UserId" = {userId} AND m."RuntimeMinutes" > 0
                    ), 0) AS "MovieKnownCount",
                    COALESCE((
                        SELECT SUM(e."RuntimeMinutes")
                        FROM watched_episodes AS we
                        INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                        WHERE we."UserId" = {userId} AND e."RuntimeMinutes" > 0
                    ), 0)::integer AS "EpisodeTotalMinutes",
                    COALESCE((
                        SELECT COUNT(*)::integer
                        FROM watched_episodes AS we
                        INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                        WHERE we."UserId" = {userId} AND e."RuntimeMinutes" > 0
                    ), 0) AS "EpisodeKnownCount"
                """)
            .FirstOrDefaultAsync(cancellationToken);

        return row ?? new V3RuntimeTotalsSqlRow();
    }

    internal static async Task<IReadOnlyList<InsightsV3GenreRatingRow>> GetGenreRatingsAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQuery<V3GenreRatingSqlRow>($"""
                SELECT
                    mg."GenreId" AS "GenreId",
                    g."Name" AS "Name",
                    r."Score" AS "Score"
                FROM ratings AS r
                INNER JOIN movies AS m ON m."Id" = r."MovieId"
                INNER JOIN movie_genres AS mg ON mg."MovieId" = m."Id"
                INNER JOIN genres AS g ON g."Id" = mg."GenreId"
                WHERE r."UserId" = {userId} AND r."MovieId" IS NOT NULL
                UNION ALL
                SELECT
                    tg."GenreId" AS "GenreId",
                    g."Name" AS "Name",
                    r."Score" AS "Score"
                FROM ratings AS r
                INNER JOIN tv_shows AS t ON t."Id" = r."TvShowId"
                INNER JOIN tv_show_genres AS tg ON tg."TvShowId" = t."Id"
                INNER JOIN genres AS g ON g."Id" = tg."GenreId"
                WHERE r."UserId" = {userId} AND r."TvShowId" IS NOT NULL
                """)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(item => new { item.GenreId, item.Name })
            .Select(group => new InsightsV3GenreRatingRow(
                group.Key.GenreId,
                group.Key.Name,
                group.Count(),
                group.Average(item => (decimal)item.Score)))
            .ToList();
    }

    internal static async Task<InsightsV3OldestTitleRow?> GetOldestTitleAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Database
            .SqlQuery<V3OldestTitleSqlRow>($"""
                SELECT
                    candidate."ContentType",
                    candidate."ContentId",
                    candidate."Title",
                    candidate."Year",
                    candidate."PosterPath",
                    candidate."SortDate"
                FROM (
                    (
                        SELECT
                            'movie' AS "ContentType",
                            wm."MovieId" AS "ContentId",
                            m."Title" AS "Title",
                            EXTRACT(YEAR FROM m."ReleaseDate")::integer AS "Year",
                            m."PosterPath" AS "PosterPath",
                            m."ReleaseDate" AS "SortDate",
                            0 AS content_rank
                        FROM watched_movies AS wm
                        INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                        WHERE wm."UserId" = {userId} AND m."ReleaseDate" IS NOT NULL
                        ORDER BY m."ReleaseDate" ASC
                        LIMIT 1
                    )
                    UNION ALL
                    (
                        SELECT
                            'tv' AS "ContentType",
                            s."TvShowId" AS "ContentId",
                            t."Title" AS "Title",
                            EXTRACT(YEAR FROM t."FirstAirDate")::integer AS "Year",
                            t."PosterPath" AS "PosterPath",
                            t."FirstAirDate" AS "SortDate",
                            1 AS content_rank
                        FROM watched_episodes AS we
                        INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                        INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                        INNER JOIN tv_shows AS t ON t."Id" = s."TvShowId"
                        WHERE we."UserId" = {userId} AND t."FirstAirDate" IS NOT NULL
                        ORDER BY t."FirstAirDate" ASC
                        LIMIT 1
                    )
                ) AS candidate
                ORDER BY candidate."SortDate" ASC, candidate.content_rank ASC
                LIMIT 1
                """)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new InsightsV3OldestTitleRow(
            row.ContentType,
            row.ContentId,
            row.Title,
            row.Year,
            row.PosterPath,
            row.SortDate);
    }

}
