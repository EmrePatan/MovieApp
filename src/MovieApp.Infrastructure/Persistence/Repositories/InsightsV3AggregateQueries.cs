using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Insights;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static class InsightsV3AggregateQueries
{
    internal sealed record GenreContributionSets(
        IReadOnlyList<InsightsV3GenreContribution> AllTime,
        IReadOnlyList<InsightsV3GenreContribution> CurrentYear,
        IReadOnlyList<InsightsV3GenreContribution> PreviousYear);

    internal sealed record TitleFacts(
        int AllTimeTitlesWithGenres,
        int CurrentYearTitlesWithGenres,
        int PreviousYearTitlesWithGenres,
        IReadOnlyList<InsightsV3ReleaseYearCount> ReleaseYears);

    private sealed class YearActivitySqlRow
    {
        public string Section { get; init; } = string.Empty;

        public int Key { get; init; }

        public int Movies { get; init; }

        public int Episodes { get; init; }

        public int Total { get; init; }

        public int ActiveDays { get; init; }

        public int YearMinutes { get; init; }
    }

    private sealed class GenreContributionSqlRow
    {
        public string Scope { get; init; } = string.Empty;

        public Guid GenreId { get; init; }

        public string Name { get; init; } = string.Empty;

        public int GenresOnTitle { get; init; }

        public int TitleCount { get; init; }
    }

    private sealed class TitleFactSqlRow
    {
        public string Section { get; init; } = string.Empty;

        public int? Year { get; init; }

        public int Count { get; init; }

        public int AllTimeTitlesWithGenres { get; init; }

        public int CurrentYearTitlesWithGenres { get; init; }

        public int PreviousYearTitlesWithGenres { get; init; }
    }

    internal static async Task<InsightsV3YearActivityAggregate> GetYearActivityAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        string timeZoneId,
        int year,
        DateTime yearStartUtc,
        DateTime yearEndUtc,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQuery<YearActivitySqlRow>($"""
                WITH local_watches AS (
                    SELECT
                        (timezone({timeZoneId}, wm."WatchedAt"))::date AS local_date,
                        'movie'::text AS kind,
                        CASE
                            WHEN m."RuntimeMinutes" > 0 THEN m."RuntimeMinutes"
                            ELSE 0
                        END AS runtime_minutes
                    FROM watched_movies AS wm
                    INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                    WHERE wm."UserId" = {userId}
                      AND wm."WatchedAt" >= {yearStartUtc}
                      AND wm."WatchedAt" < {yearEndUtc}
                    UNION ALL
                    SELECT
                        (timezone({timeZoneId}, we."WatchedAt"))::date AS local_date,
                        'episode'::text AS kind,
                        CASE
                            WHEN e."RuntimeMinutes" > 0 THEN e."RuntimeMinutes"
                            ELSE 0
                        END AS runtime_minutes
                    FROM watched_episodes AS we
                    INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                    WHERE we."UserId" = {userId}
                      AND we."WatchedAt" >= {yearStartUtc}
                      AND we."WatchedAt" < {yearEndUtc}
                ),
                in_year AS (
                    SELECT local_date, kind, runtime_minutes
                    FROM local_watches
                    WHERE EXTRACT(YEAR FROM local_date)::integer = {year}
                )
                SELECT
                    'month'::text AS "Section",
                    EXTRACT(MONTH FROM local_date)::integer AS "Key",
                    COUNT(*) FILTER (WHERE kind = 'movie')::integer AS "Movies",
                    COUNT(*) FILTER (WHERE kind = 'episode')::integer AS "Episodes",
                    COUNT(*)::integer AS "Total",
                    0 AS "ActiveDays",
                    0 AS "YearMinutes"
                FROM in_year
                GROUP BY EXTRACT(MONTH FROM local_date)
                UNION ALL
                SELECT
                    'weekday'::text AS "Section",
                    EXTRACT(DOW FROM local_date)::integer AS "Key",
                    0 AS "Movies",
                    0 AS "Episodes",
                    COUNT(*)::integer AS "Total",
                    0 AS "ActiveDays",
                    0 AS "YearMinutes"
                FROM in_year
                GROUP BY EXTRACT(DOW FROM local_date)
                UNION ALL
                SELECT
                    'totals'::text AS "Section",
                    0 AS "Key",
                    0 AS "Movies",
                    0 AS "Episodes",
                    0 AS "Total",
                    COUNT(DISTINCT local_date)::integer AS "ActiveDays",
                    COALESCE(SUM(runtime_minutes), 0)::integer AS "YearMinutes"
                FROM in_year
                """)
            .ToListAsync(cancellationToken);

        var months = rows
            .Where(row => row.Section == "month")
            .Select(row => new InsightsV3MonthCount(row.Key, row.Movies, row.Episodes))
            .ToList();
        var weekdayTotals = new Dictionary<DayOfWeek, int>();
        foreach (var row in rows.Where(row => row.Section == "weekday"))
        {
            if (Enum.IsDefined(typeof(DayOfWeek), row.Key))
            {
                weekdayTotals[(DayOfWeek)row.Key] = row.Total;
            }
        }

        var totals = rows.FirstOrDefault(row => row.Section == "totals");
        return new InsightsV3YearActivityAggregate(
            months,
            weekdayTotals,
            totals?.ActiveDays ?? 0,
            totals?.YearMinutes ?? 0);
    }

    internal static async Task<GenreContributionSets> GetGenreContributionsAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        DateTime previousYearStartUtc,
        DateTime previousYearEndUtc,
        DateTime currentYearStartUtc,
        DateTime currentYearEndUtc,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQuery<GenreContributionSqlRow>($"""
                SELECT
                    weighted.scope AS "Scope",
                    weighted.genre_id AS "GenreId",
                    weighted.genre_name AS "Name",
                    weighted.genre_count AS "GenresOnTitle",
                    COUNT(*)::integer AS "TitleCount"
                FROM (
                    SELECT
                        'all_time'::text AS scope,
                        mg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY wm."Id")::integer AS genre_count
                    FROM watched_movies AS wm
                    INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                    INNER JOIN movie_genres AS mg ON mg."MovieId" = m."Id"
                    INNER JOIN genres AS g ON g."Id" = mg."GenreId"
                    WHERE wm."UserId" = {userId}
                    UNION ALL
                    SELECT
                        'all_time'::text AS scope,
                        tg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY shows.show_id)::integer AS genre_count
                    FROM (
                        SELECT DISTINCT s."TvShowId" AS show_id
                        FROM watched_episodes AS we
                        INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                        INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                        WHERE we."UserId" = {userId}
                    ) AS shows
                    INNER JOIN tv_show_genres AS tg ON tg."TvShowId" = shows.show_id
                    INNER JOIN genres AS g ON g."Id" = tg."GenreId"
                    UNION ALL
                    SELECT
                        'current_year'::text AS scope,
                        mg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY wm."Id")::integer AS genre_count
                    FROM watched_movies AS wm
                    INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                    INNER JOIN movie_genres AS mg ON mg."MovieId" = m."Id"
                    INNER JOIN genres AS g ON g."Id" = mg."GenreId"
                    WHERE wm."UserId" = {userId}
                      AND wm."WatchedAt" >= {currentYearStartUtc}
                      AND wm."WatchedAt" < {currentYearEndUtc}
                    UNION ALL
                    SELECT
                        'previous_year'::text AS scope,
                        mg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY wm."Id")::integer AS genre_count
                    FROM watched_movies AS wm
                    INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                    INNER JOIN movie_genres AS mg ON mg."MovieId" = m."Id"
                    INNER JOIN genres AS g ON g."Id" = mg."GenreId"
                    WHERE wm."UserId" = {userId}
                      AND wm."WatchedAt" >= {previousYearStartUtc}
                      AND wm."WatchedAt" < {previousYearEndUtc}
                    UNION ALL
                    SELECT
                        'current_year'::text AS scope,
                        tg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY we."Id")::integer AS genre_count
                    FROM watched_episodes AS we
                    INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                    INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                    INNER JOIN tv_show_genres AS tg ON tg."TvShowId" = s."TvShowId"
                    INNER JOIN genres AS g ON g."Id" = tg."GenreId"
                    WHERE we."UserId" = {userId}
                      AND we."WatchedAt" >= {currentYearStartUtc}
                      AND we."WatchedAt" < {currentYearEndUtc}
                    UNION ALL
                    SELECT
                        'previous_year'::text AS scope,
                        tg."GenreId" AS genre_id,
                        g."Name" AS genre_name,
                        COUNT(*) OVER (PARTITION BY we."Id")::integer AS genre_count
                    FROM watched_episodes AS we
                    INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                    INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                    INNER JOIN tv_show_genres AS tg ON tg."TvShowId" = s."TvShowId"
                    INNER JOIN genres AS g ON g."Id" = tg."GenreId"
                    WHERE we."UserId" = {userId}
                      AND we."WatchedAt" >= {previousYearStartUtc}
                      AND we."WatchedAt" < {previousYearEndUtc}
                ) AS weighted
                GROUP BY weighted.scope, weighted.genre_id, weighted.genre_name, weighted.genre_count
                """)
            .ToListAsync(cancellationToken);

        return new GenreContributionSets(
            ToContributions(rows, "all_time"),
            ToContributions(rows, "current_year"),
            ToContributions(rows, "previous_year"));
    }

    internal static async Task<TitleFacts> GetTitleFactsAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        DateTime previousYearStartUtc,
        DateTime previousYearEndUtc,
        DateTime currentYearStartUtc,
        DateTime currentYearEndUtc,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Database
            .SqlQuery<TitleFactSqlRow>($"""
                WITH watched_shows AS (
                    SELECT DISTINCT s."TvShowId" AS show_id
                    FROM watched_episodes AS we
                    INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                    INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                    WHERE we."UserId" = {userId}
                ),
                release_years AS (
                    SELECT EXTRACT(YEAR FROM m."ReleaseDate")::integer AS release_year
                    FROM watched_movies AS wm
                    INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                    WHERE wm."UserId" = {userId}
                    UNION ALL
                    SELECT EXTRACT(YEAR FROM t."FirstAirDate")::integer AS release_year
                    FROM watched_shows AS ws
                    INNER JOIN tv_shows AS t ON t."Id" = ws.show_id
                )
                SELECT
                    'facts'::text AS "Section",
                    NULL::integer AS "Year",
                    0 AS "Count",
                    (
                        (SELECT COUNT(*)::integer
                         FROM watched_movies AS wm
                         INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                         WHERE wm."UserId" = {userId}
                           AND EXISTS (
                               SELECT 1
                               FROM movie_genres AS mg
                               WHERE mg."MovieId" = m."Id"))
                        +
                        (SELECT COUNT(*)::integer
                         FROM watched_shows AS ws
                         WHERE EXISTS (
                             SELECT 1
                             FROM tv_show_genres AS tg
                             WHERE tg."TvShowId" = ws.show_id))
                    ) AS "AllTimeTitlesWithGenres",
                    (
                        (SELECT COUNT(*)::integer
                         FROM watched_movies AS wm
                         INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                         WHERE wm."UserId" = {userId}
                           AND wm."WatchedAt" >= {currentYearStartUtc}
                           AND wm."WatchedAt" < {currentYearEndUtc}
                           AND EXISTS (
                               SELECT 1
                               FROM movie_genres AS mg
                               WHERE mg."MovieId" = m."Id"))
                        +
                        (SELECT COUNT(*)::integer
                         FROM watched_episodes AS we
                         INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                         INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                         WHERE we."UserId" = {userId}
                           AND we."WatchedAt" >= {currentYearStartUtc}
                           AND we."WatchedAt" < {currentYearEndUtc}
                           AND EXISTS (
                               SELECT 1
                               FROM tv_show_genres AS tg
                               WHERE tg."TvShowId" = s."TvShowId"))
                    ) AS "CurrentYearTitlesWithGenres",
                    (
                        (SELECT COUNT(*)::integer
                         FROM watched_movies AS wm
                         INNER JOIN movies AS m ON m."Id" = wm."MovieId"
                         WHERE wm."UserId" = {userId}
                           AND wm."WatchedAt" >= {previousYearStartUtc}
                           AND wm."WatchedAt" < {previousYearEndUtc}
                           AND EXISTS (
                               SELECT 1
                               FROM movie_genres AS mg
                               WHERE mg."MovieId" = m."Id"))
                        +
                        (SELECT COUNT(*)::integer
                         FROM watched_episodes AS we
                         INNER JOIN episodes AS e ON e."Id" = we."EpisodeId"
                         INNER JOIN seasons AS s ON s."Id" = e."SeasonId"
                         WHERE we."UserId" = {userId}
                           AND we."WatchedAt" >= {previousYearStartUtc}
                           AND we."WatchedAt" < {previousYearEndUtc}
                           AND EXISTS (
                               SELECT 1
                               FROM tv_show_genres AS tg
                               WHERE tg."TvShowId" = s."TvShowId"))
                    ) AS "PreviousYearTitlesWithGenres"
                UNION ALL
                SELECT
                    'year'::text AS "Section",
                    release_years.release_year AS "Year",
                    COUNT(*)::integer AS "Count",
                    0 AS "AllTimeTitlesWithGenres",
                    0 AS "CurrentYearTitlesWithGenres",
                    0 AS "PreviousYearTitlesWithGenres"
                FROM release_years
                GROUP BY release_years.release_year
                """)
            .ToListAsync(cancellationToken);

        var facts = rows.Single(row => row.Section == "facts");
        var releaseYears = rows
            .Where(row => row.Section == "year")
            .Select(row => new InsightsV3ReleaseYearCount(row.Year, row.Count))
            .ToList();

        return new TitleFacts(
            facts.AllTimeTitlesWithGenres,
            facts.CurrentYearTitlesWithGenres,
            facts.PreviousYearTitlesWithGenres,
            releaseYears);
    }

    private static List<InsightsV3GenreContribution> ToContributions(
        IReadOnlyList<GenreContributionSqlRow> rows,
        string scope) =>
        rows
            .Where(row => row.Scope == scope)
            .Select(row => new InsightsV3GenreContribution(
                row.GenreId,
                row.Name,
                row.GenresOnTitle,
                row.TitleCount))
            .ToList();
}
