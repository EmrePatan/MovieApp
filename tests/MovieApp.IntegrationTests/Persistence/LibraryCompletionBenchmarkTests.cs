using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Search;
using MovieApp.Infrastructure.Persistence;
using MovieApp.Infrastructure.Persistence.Repositories;
using Npgsql;

namespace MovieApp.IntegrationTests.Persistence;

/// <summary>
/// Seeds a catalog the size of the reported regression (~200k episodes, ~16k watches, 45 started shows)
/// and checks that the shipped started-show load matches pre-#18 order and is clearly faster.
/// </summary>
[Collection("LibraryCompletionBenchmark")]
public sealed class LibraryCompletionBenchmarkTests : IAsyncLifetime
{
    private const string DatabaseName = "movieapp_library_completion_bench";

    private ApplicationDbContext context = null!;
    private Guid userId;

    public async Task InitializeAsync()
    {
        await DropDatabaseAsync();
        context = CreateContext();
        await context.Database.MigrateAsync();
        await SeedAsync();
        userId = await ScalarAsync<Guid>("SELECT bench_uuid(1)");
    }

    public async Task DisposeAsync()
    {
        await context.DisposeAsync();
        await DropDatabaseAsync();
    }

    [Fact]
    public async Task ShippedStartedShowsMatchPre18OrderAndBeatItAtScale()
    {
        var episodes = await ScalarAsync<long>("SELECT count(*) FROM episodes");
        var watches = await ScalarAsync<long>(
            "SELECT count(*) FROM watched_episodes WHERE \"UserId\" = bench_uuid(1)");
        Assert.True(episodes >= 200_000, $"expected at least 200k episodes, found {episodes}");
        Assert.True(watches >= 16_000, $"expected at least 16k watches, found {watches}");

        var repository = new LibraryRepository(context);
        var watchingRequest = new LibraryPageRequest(1, 24, 25, null, LibraryCountMode.Required);
        var (watchingItems, watchingTotal) = await repository.GetWatchingAsync(
            userId,
            SearchContentType.All,
            watchingRequest);
        var watchedRequest = new LibraryPageRequest(1, 50, 51, null, LibraryCountMode.Required);
        var (watchedItems, watchedTotal) = await repository.GetWatchedAsync(
            userId,
            SearchContentType.Tv,
            watchedRequest);

        Assert.Equal(16, watchingTotal);
        Assert.Equal(16, watchingItems.Count);
        Assert.Equal(29, watchedTotal);
        Assert.Equal(29, watchedItems.Count);

        var preWatchingIds = await QueryIdsAsync(PreWatchingPageSql, 24);
        var preWatchedIds = await QueryIdsAsync(PreWatchedTvPageSql, 50);
        Assert.Equal(preWatchingIds, watchingItems.Select(item => item.Id).ToList());
        Assert.Equal(preWatchedIds, watchedItems.Select(item => item.Id).ToList());

        var pgOrdersHighBitLast = await ScalarAsync<bool>("""
            SELECT 'ffffffff-0000-0000-0000-000000000001'::uuid
                 > '7fffffff-0000-0000-0000-000000000001'::uuid
            """);
        Assert.True(pgOrdersHighBitLast);
        Assert.True(PostgresGuidComparer.Instance.Compare(
            Guid.Parse("7fffffff-0000-0000-0000-000000000001"),
            Guid.Parse("ffffffff-0000-0000-0000-000000000001")) < 0);

        var startedShowsSql = string.Join(
            '\n',
            TvShowCompletionQueries.StartedShows(context, userId)
                .ToQueryString()
                .Split('\n')
                .Where(line => !line.StartsWith("--", StringComparison.Ordinal)));
        startedShowsSql = startedShowsSql.Replace("@p0", $"'{userId}'", StringComparison.Ordinal);
        var plan = await QueryTextAsync("EXPLAIN (ANALYZE, BUFFERS) " + startedShowsSql);
        Assert.DoesNotContain("Seq Scan on episodes", plan, StringComparison.Ordinal);
        Assert.Contains("Index Scan", plan, StringComparison.Ordinal);

        var preMs = await TimeQueryAsync(PreWatchingPageSql, 24);
        var groupedMs = await TimeQueryAsync(GroupedWatchingPageSql, 24);
        var shippedMs = await TimeAsync(async () =>
        {
            await using var timed = CreateContext();
            _ = await TvShowCompletionQueries.LoadStartedShowsAsync(timed, userId, CancellationToken.None);
        });

        Assert.True(
            shippedMs * 2 < preMs,
            $"shipped started-show load ({shippedMs:0} ms) was not clearly faster than pre-#18 ({preMs:0} ms); #18 grouped SQL was {groupedMs:0} ms");
    }

    private async Task SeedAsync()
    {
        await context.Database.ExecuteSqlRawAsync(SeedSql);
    }

    private async Task<List<Guid>> QueryIdsAsync(string sql, int limit)
    {
        var ids = new List<Guid>();
        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        AddUserParameters(command, limit);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private async Task<string> QueryTextAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }

        return string.Join('\n', lines);
    }

    private async Task<double> TimeQueryAsync(string sql, int limit)
    {
        return await TimeAsync(async () =>
        {
            await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            AddUserParameters(command, limit);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
            }
        });
    }

    private static async Task<double> TimeAsync(Func<Task> action)
    {
        await action();
        var best = double.MaxValue;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            await action();
            stopwatch.Stop();
            best = Math.Min(best, stopwatch.Elapsed.TotalMilliseconds);
        }

        return best;
    }

    private void AddUserParameters(NpgsqlCommand command, int limit)
    {
        command.Parameters.AddWithValue("userId", userId);
        command.Parameters.AddWithValue("p", limit);
        command.Parameters.AddWithValue("p0", userId);
        command.Parameters.AddWithValue("p1", userId);
        command.Parameters.AddWithValue("p2", userId);
        command.Parameters.AddWithValue("p10", limit);
    }

    private async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync();
        return (T)value!;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(IntegrationTestDatabase.GetConnectionString(DatabaseName))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task DropDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(IntegrationTestDatabase.GetConnectionString("postgres"));
        await connection.OpenAsync();
        await using (var terminate = new NpgsqlCommand(
            $"""
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = '{DatabaseName}' AND pid <> pg_backend_pid()
            """,
            connection))
        {
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"""DROP DATABASE IF EXISTS "{DatabaseName}" """, connection);
        await drop.ExecuteNonQueryAsync();
    }

    private const string SeedSql = """
        CREATE FUNCTION bench_uuid(n bigint) RETURNS uuid
        LANGUAGE sql IMMUTABLE AS $$
            SELECT (
                lpad(to_hex((n >> 64) & 4294967295), 8, '0') || '-' ||
                lpad(to_hex((n >> 48) & 65535), 4, '0') || '-' ||
                lpad(to_hex((n >> 32) & 65535), 4, '0') || '-' ||
                lpad(to_hex((n >> 16) & 65535), 4, '0') || '-' ||
                lpad(to_hex(n & 65535), 12, '0')
            )::uuid
        $$;

        INSERT INTO users (
            "Id", "Email", "NormalizedEmail", "UserName", "DisplayName",
            "PasswordHash", "IsActive", "CreatedAt", "UpdatedAt", "SecurityStamp")
        VALUES (
            bench_uuid(1), 'bench@example.com', 'BENCH@EXAMPLE.COM', 'bench', 'bench',
            'hash', true, TIMESTAMPTZ '2024-01-01', TIMESTAMPTZ '2024-01-01', bench_uuid(2));

        INSERT INTO tv_shows ("Id", "Title", "Status", "VoteAverage", "VoteCount", "CreatedAt", "UpdatedAt")
        SELECT bench_uuid(i),
               'Background ' || i,
               CASE WHEN i % 4 = 0 THEN 'Ended' ELSE 'ReturningSeries' END,
               0, 0, TIMESTAMPTZ '2024-01-01', TIMESTAMPTZ '2024-01-01'
        FROM generate_series(1, 5300) AS i;

        INSERT INTO seasons ("Id", "TvShowId", "SeasonNumber", "EpisodeCount", "CreatedAt", "UpdatedAt")
        SELECT bench_uuid(1000000 + (i - 1) * 3 + s),
               bench_uuid(i), s, 12, TIMESTAMPTZ '2024-01-01', TIMESTAMPTZ '2024-01-01'
        FROM generate_series(1, 5300) AS i
        CROSS JOIN generate_series(1, 3) AS s;

        INSERT INTO episodes (
            "Id", "SeasonId", "EpisodeNumber", "VoteAverage", "VoteCount", "CreatedAt", "UpdatedAt")
        SELECT bench_uuid(20000000 + (season_ord - 1) * 12 + e),
               bench_uuid(1000000 + season_ord),
               e, 0, 0, TIMESTAMPTZ '2024-01-01', TIMESTAMPTZ '2024-01-01'
        FROM generate_series(1, 5300 * 3) AS season_ord
        CROSS JOIN generate_series(1, 12) AS e;

        INSERT INTO tv_shows ("Id", "Title", "Status", "VoteAverage", "VoteCount", "CreatedAt", "UpdatedAt")
        SELECT bench_uuid(100000 + i),
               'Started ' || i,
               CASE WHEN i <= 29 THEN 'Ended' ELSE 'ReturningSeries' END,
               0, 0, TIMESTAMPTZ '2024-01-01', TIMESTAMPTZ '2024-01-01'
        FROM generate_series(1, 45) AS i;

        INSERT INTO seasons ("Id", "TvShowId", "SeasonNumber", "EpisodeCount", "CreatedAt", "UpdatedAt")
        SELECT bench_uuid(5000000 + (i - 1) * 11 + s),
               bench_uuid(100000 + i),
               s,
               CASE WHEN s < 10 THEN 40 WHEN i <= 29 THEN 0 ELSE 6 END,
               TIMESTAMPTZ '2024-01-01', TIMESTAMPTZ '2024-01-01'
        FROM generate_series(1, 45) AS i
        CROSS JOIN generate_series(1, 10) AS s;

        INSERT INTO episodes (
            "Id", "SeasonId", "EpisodeNumber", "VoteAverage", "VoteCount", "CreatedAt", "UpdatedAt")
        SELECT bench_uuid(40000000 + (i - 1) * 360 + (s - 1) * 40 + e),
               bench_uuid(5000000 + (i - 1) * 11 + s),
               e, 0, 0, TIMESTAMPTZ '2024-01-01', TIMESTAMPTZ '2024-01-01'
        FROM generate_series(1, 45) AS i
        CROSS JOIN generate_series(1, 9) AS s
        CROSS JOIN generate_series(1, 40) AS e;

        INSERT INTO watched_episodes ("Id", "UserId", "EpisodeId", "WatchedAt", "CreatedAt", "UpdatedAt")
        SELECT bench_uuid(80000000 + row_number() OVER ()),
               bench_uuid(1),
               episode_id,
               TIMESTAMPTZ '2024-01-01' + (row_number() OVER (ORDER BY episode_id) * INTERVAL '1 minute'),
               TIMESTAMPTZ '2024-01-01',
               TIMESTAMPTZ '2024-01-01'
        FROM (
            SELECT bench_uuid(40000000 + (i - 1) * 360 + (s - 1) * 40 + e) AS episode_id
            FROM generate_series(1, 45) AS i
            CROSS JOIN generate_series(1, 8) AS s
            CROSS JOIN generate_series(1, 40) AS e
            UNION ALL
            SELECT bench_uuid(40000000 + (i - 1) * 360 + 8 * 40 + e)
            FROM generate_series(1, 45) AS i
            CROSS JOIN generate_series(1, 35) AS e
            UNION ALL
            SELECT bench_uuid(40000000 + (i - 1) * 360 + 320 + e)
            FROM generate_series(1, 29) AS i
            CROSS JOIN generate_series(36, 40) AS e
        ) AS watched;

        ANALYZE tv_shows;
        ANALYZE seasons;
        ANALYZE episodes;
        ANALYZE watched_episodes;
        """;

    // EF Core SQL for the pre-#18 watching page (correlated subqueries repeated in the filter and sort).
    private const string PreWatchingPageSql = """
        SELECT t0."Id"
        FROM tv_shows AS t
        INNER JOIN tv_shows AS t0 ON t."Id" = t0."Id"
        WHERE t."Id" IN (
                SELECT s."TvShowId"
                FROM watched_episodes AS w
                INNER JOIN episodes AS e ON w."EpisodeId" = e."Id"
                INNER JOIN seasons AS s ON e."SeasonId" = s."Id"
                WHERE w."UserId" = @userId AND s."SeasonNumber" >= 1
            ) AND (t."Status" NOT IN ('Ended', 'Canceled') OR CASE
                WHEN (
                    SELECT count(*)::int
                    FROM episodes AS e0
                    INNER JOIN seasons AS s0 ON e0."SeasonId" = s0."Id"
                    WHERE s0."TvShowId" = t."Id" AND s0."SeasonNumber" >= 1) + (
                    SELECT COALESCE(sum(COALESCE(s1."EpisodeCount", 0)), 0)::int
                    FROM seasons AS s1
                    WHERE t."Id" = s1."TvShowId" AND s1."SeasonNumber" >= 1 AND (
                        SELECT count(*)::int
                        FROM episodes AS e1
                        WHERE s1."Id" = e1."SeasonId") = 0) > 0 THEN FALSE
                ELSE TRUE
            END OR CASE
                WHEN (
                    SELECT count(*)::int
                    FROM watched_episodes AS w0
                    INNER JOIN episodes AS e2 ON w0."EpisodeId" = e2."Id"
                    INNER JOIN seasons AS s2 ON e2."SeasonId" = s2."Id"
                    WHERE w0."UserId" = @userId AND s2."TvShowId" = t."Id" AND s2."SeasonNumber" >= 1) >= (
                    SELECT count(*)::int
                    FROM episodes AS e3
                    INNER JOIN seasons AS s3 ON e3."SeasonId" = s3."Id"
                    WHERE s3."TvShowId" = t."Id" AND s3."SeasonNumber" >= 1) + (
                    SELECT COALESCE(sum(COALESCE(s4."EpisodeCount", 0)), 0)::int
                    FROM seasons AS s4
                    WHERE t."Id" = s4."TvShowId" AND s4."SeasonNumber" >= 1 AND (
                        SELECT count(*)::int
                        FROM episodes AS e4
                        WHERE s4."Id" = e4."SeasonId") = 0) THEN FALSE
                ELSE TRUE
            END)
            ORDER BY CASE
                WHEN (
                    SELECT count(*)::int
                    FROM watched_episodes AS w1
                    INNER JOIN episodes AS e5 ON w1."EpisodeId" = e5."Id"
                    INNER JOIN seasons AS s5 ON e5."SeasonId" = s5."Id"
                    WHERE w1."UserId" = @userId AND s5."TvShowId" = t."Id" AND s5."SeasonNumber" >= 1) < (
                    SELECT count(*)::int
                    FROM episodes AS e6
                    INNER JOIN seasons AS s6 ON e6."SeasonId" = s6."Id"
                    WHERE s6."TvShowId" = t."Id" AND s6."SeasonNumber" >= 1) + (
                    SELECT COALESCE(sum(COALESCE(s7."EpisodeCount", 0)), 0)::int
                    FROM seasons AS s7
                    WHERE t."Id" = s7."TvShowId" AND s7."SeasonNumber" >= 1 AND (
                        SELECT count(*)::int
                        FROM episodes AS e7
                        WHERE s7."Id" = e7."SeasonId") = 0) THEN TRUE
                ELSE FALSE
            END DESC, (
                SELECT max(w2."WatchedAt")
                FROM watched_episodes AS w2
                INNER JOIN episodes AS e8 ON w2."EpisodeId" = e8."Id"
                INNER JOIN seasons AS s8 ON e8."SeasonId" = s8."Id"
                WHERE w2."UserId" = @userId AND s8."TvShowId" = t."Id" AND s8."SeasonNumber" >= 1) DESC, t0."Id"
        LIMIT @p
        """;

    private const string PreWatchedTvPageSql = """
        SELECT t0."Id"
        FROM tv_shows AS t
        INNER JOIN tv_shows AS t0 ON t."Id" = t0."Id"
        WHERE t."Id" IN (
            SELECT s."TvShowId"
            FROM watched_episodes AS w
            INNER JOIN episodes AS e ON w."EpisodeId" = e."Id"
            INNER JOIN seasons AS s ON e."SeasonId" = s."Id"
            WHERE w."UserId" = @userId AND s."SeasonNumber" >= 1
        ) AND t."Status" IN ('Ended', 'Canceled') AND (
            SELECT count(*)::int
            FROM episodes AS e0
            INNER JOIN seasons AS s0 ON e0."SeasonId" = s0."Id"
            WHERE s0."TvShowId" = t."Id" AND s0."SeasonNumber" >= 1) + (
            SELECT COALESCE(sum(COALESCE(s1."EpisodeCount", 0)), 0)::int
            FROM seasons AS s1
            WHERE t."Id" = s1."TvShowId" AND s1."SeasonNumber" >= 1 AND (
                SELECT count(*)::int
                FROM episodes AS e1
                WHERE s1."Id" = e1."SeasonId") = 0) > 0 AND (
            SELECT count(*)::int
            FROM watched_episodes AS w0
            INNER JOIN episodes AS e2 ON w0."EpisodeId" = e2."Id"
            INNER JOIN seasons AS s2 ON e2."SeasonId" = s2."Id"
            WHERE w0."UserId" = @userId AND s2."TvShowId" = t."Id" AND s2."SeasonNumber" >= 1) >= (
            SELECT count(*)::int
            FROM episodes AS e3
            INNER JOIN seasons AS s3 ON e3."SeasonId" = s3."Id"
            WHERE s3."TvShowId" = t."Id" AND s3."SeasonNumber" >= 1) + (
            SELECT COALESCE(sum(COALESCE(s4."EpisodeCount", 0)), 0)::int
            FROM seasons AS s4
            WHERE t."Id" = s4."TvShowId" AND s4."SeasonNumber" >= 1 AND (
                SELECT count(*)::int
                FROM episodes AS e4
                WHERE s4."Id" = e4."SeasonId") = 0) AND (
            SELECT max(w1."WatchedAt")
            FROM watched_episodes AS w1
            INNER JOIN episodes AS e5 ON w1."EpisodeId" = e5."Id"
            INNER JOIN seasons AS s5 ON e5."SeasonId" = s5."Id"
            WHERE w1."UserId" = @userId AND s5."TvShowId" = t."Id" AND s5."SeasonNumber" >= 1) IS NOT NULL
        ORDER BY (
            SELECT max(w2."WatchedAt")
            FROM watched_episodes AS w2
            INNER JOIN episodes AS e6 ON w2."EpisodeId" = e6."Id"
            INNER JOIN seasons AS s6 ON e6."SeasonId" = s6."Id"
            WHERE w2."UserId" = @userId AND s6."TvShowId" = t."Id" AND s6."SeasonNumber" >= 1) DESC, t0."Id"
        LIMIT @p
        """;

    // EF Core SQL for the #18 grouped watching page.
    private const string GroupedWatchingPageSql = """
        SELECT t."Id"
        FROM (
            SELECT
                show."Id" AS "TvShowId",
                (show."Status" IN ('Ended', 'Canceled')) AS "IsConcluded",
                (COALESCE(episode_rows.episode_rows, 0) + COALESCE(empty_seasons.placeholder_episodes, 0)) AS "RegularTotalEpisodes",
                watched.watched_count AS "RegularWatchedEpisodes",
                watched.last_watched_at AS "LastWatchedAt"
            FROM (
                SELECT
                    season."TvShowId" AS tv_show_id,
                    COUNT(*)::integer AS watched_count,
                    MAX(watched_episode."WatchedAt") AS last_watched_at
                FROM watched_episodes AS watched_episode
                INNER JOIN episodes AS episode ON episode."Id" = watched_episode."EpisodeId"
                INNER JOIN seasons AS season ON season."Id" = episode."SeasonId"
                WHERE watched_episode."UserId" = @p0
                  AND season."SeasonNumber" >= 1
                GROUP BY season."TvShowId"
            ) AS watched
            INNER JOIN tv_shows AS show ON show."Id" = watched.tv_show_id
            LEFT JOIN (
                SELECT
                    season."TvShowId" AS tv_show_id,
                    COUNT(*)::integer AS episode_rows
                FROM episodes AS episode
                INNER JOIN seasons AS season ON season."Id" = episode."SeasonId"
                WHERE season."SeasonNumber" >= 1
                  AND season."TvShowId" IN (
                      SELECT started_season."TvShowId"
                      FROM watched_episodes AS started_watch
                      INNER JOIN episodes AS started_episode ON started_episode."Id" = started_watch."EpisodeId"
                      INNER JOIN seasons AS started_season ON started_season."Id" = started_episode."SeasonId"
                      WHERE started_watch."UserId" = @p1
                        AND started_season."SeasonNumber" >= 1
                  )
                GROUP BY season."TvShowId"
            ) AS episode_rows ON episode_rows.tv_show_id = watched.tv_show_id
            LEFT JOIN (
                SELECT
                    season."TvShowId" AS tv_show_id,
                    COALESCE(SUM(COALESCE(season."EpisodeCount", 0)), 0)::integer AS placeholder_episodes
                FROM seasons AS season
                WHERE season."SeasonNumber" >= 1
                  AND NOT EXISTS (
                      SELECT 1
                      FROM episodes AS episode
                      WHERE episode."SeasonId" = season."Id"
                  )
                  AND season."TvShowId" IN (
                      SELECT started_season."TvShowId"
                      FROM watched_episodes AS started_watch
                      INNER JOIN episodes AS started_episode ON started_episode."Id" = started_watch."EpisodeId"
                      INNER JOIN seasons AS started_season ON started_season."Id" = started_episode."SeasonId"
                      WHERE started_watch."UserId" = @p2
                        AND started_season."SeasonNumber" >= 1
                  )
                GROUP BY season."TvShowId"
            ) AS empty_seasons ON empty_seasons.tv_show_id = watched.tv_show_id
        ) AS m
        INNER JOIN tv_shows AS t ON m."TvShowId" = t."Id"
        WHERE NOT (m."IsConcluded") OR m."RegularTotalEpisodes" <= 0 OR m."RegularWatchedEpisodes" < m."RegularTotalEpisodes"
        ORDER BY m."RegularWatchedEpisodes" < m."RegularTotalEpisodes" DESC, m."LastWatchedAt" DESC, t."Id"
        LIMIT @p10
        """;
}
