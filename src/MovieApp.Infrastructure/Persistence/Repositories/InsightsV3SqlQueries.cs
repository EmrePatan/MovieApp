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

}
