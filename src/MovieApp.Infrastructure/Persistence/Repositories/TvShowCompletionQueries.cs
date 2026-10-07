using Microsoft.EntityFrameworkCore;
using MovieApp.Domain.Enums;

namespace MovieApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// SQL-translatable form of <see cref="MovieApp.Application.Services.WatchHistory.TvShowCompletionPolicy"/>.
/// Library watching sort uses <c>RegularWatchedEpisodes &lt; RegularTotalEpisodes</c>
/// (see <see cref="MovieApp.Application.Services.WatchHistory.TvShowCompletionPolicy.IsWatchingLibrarySortInProgress"/>).
/// </summary>
internal static class TvShowCompletionQueries
{
    /// <summary>
    /// One row per show in which the user has watched at least one regular (season ≥ 1) episode.
    /// Relational providers load the user's rows once with indexed lookups. Callers filter, sort,
    /// and page that small list in memory. The in-memory provider keeps the original correlated
    /// shape so unit tests stay equivalent.
    /// </summary>
    internal static async Task<List<TvShowCompletionRow>> LoadStartedShowsAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await StartedShowsInMemory(dbContext, userId).ToListAsync(cancellationToken);
        }

        return await StartedShows(dbContext, userId).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Relational started-show statement. Exposed so tests can inspect the plan shape.
    /// Do not compose further LINQ on this query: EF would wrap it and run it again for count and page.
    /// </summary>
    internal static IQueryable<TvShowCompletionRow> StartedShows(ApplicationDbContext dbContext, Guid userId)
    {
        if (!dbContext.Database.IsRelational())
        {
            return StartedShowsInMemory(dbContext, userId);
        }

        // Totals match the previous correlated projection exactly:
        // episode rows in seasons >= 1, plus EpisodeCount (null as 0, negatives preserved)
        // for regular seasons that have no ingested episodes. Season 0 is excluded.
        // LATERAL LIMIT 1 keeps the user's watches on primary-key lookups instead of hashing
        // the global episodes table.
        return dbContext.Database.SqlQuery<TvShowCompletionRow>($"""
            WITH watched AS MATERIALIZED (
                SELECT
                    season."TvShowId" AS tv_show_id,
                    COUNT(*)::integer AS watched_count,
                    MAX(watched_episode."WatchedAt") AS last_watched_at
                FROM watched_episodes AS watched_episode
                JOIN LATERAL (
                    SELECT episode."SeasonId"
                    FROM episodes AS episode
                    WHERE episode."Id" = watched_episode."EpisodeId"
                    LIMIT 1
                ) AS episode ON true
                JOIN LATERAL (
                    SELECT season."TvShowId"
                    FROM seasons AS season
                    WHERE season."Id" = episode."SeasonId"
                      AND season."SeasonNumber" >= 1
                    LIMIT 1
                ) AS season ON true
                WHERE watched_episode."UserId" = {userId}
                GROUP BY season."TvShowId"
            )
            SELECT
                show."Id" AS "TvShowId",
                (show."Status" IN ('Ended', 'Canceled')) AS "IsConcluded",
                COALESCE(totals.regular_total, 0) AS "RegularTotalEpisodes",
                watched.watched_count AS "RegularWatchedEpisodes",
                watched.last_watched_at AS "LastWatchedAt"
            FROM watched
            INNER JOIN tv_shows AS show ON show."Id" = watched.tv_show_id
            LEFT JOIN LATERAL (
                SELECT SUM(
                    CASE
                        WHEN episode_count.cnt > 0 THEN episode_count.cnt
                        ELSE COALESCE(season."EpisodeCount", 0)
                    END
                )::integer AS regular_total
                FROM seasons AS season
                JOIN LATERAL (
                    SELECT COUNT(*)::integer AS cnt
                    FROM episodes AS episode
                    WHERE episode."SeasonId" = season."Id"
                ) AS episode_count ON true
                WHERE season."TvShowId" = watched.tv_show_id
                  AND season."SeasonNumber" >= 1
            ) AS totals ON true
            """);
    }

    private static IQueryable<TvShowCompletionRow> StartedShowsInMemory(ApplicationDbContext dbContext, Guid userId)
    {
        var startedShowIds = dbContext.WatchedEpisodes
            .AsNoTracking()
            .Where(watchedEpisode =>
                watchedEpisode.UserId == userId &&
                watchedEpisode.Episode.Season.SeasonNumber >= 1)
            .Select(watchedEpisode => watchedEpisode.Episode.Season.TvShowId)
            .Distinct();

        return dbContext.TvShows
            .AsNoTracking()
            .Where(tvShow => startedShowIds.Contains(tvShow.Id))
            .Select(tvShow => new TvShowCompletionRow
            {
                TvShowId = tvShow.Id,
                IsConcluded = tvShow.Status == TvShowStatus.Ended || tvShow.Status == TvShowStatus.Canceled,
                RegularTotalEpisodes =
                    dbContext.Episodes.Count(episode =>
                        episode.Season.TvShowId == tvShow.Id &&
                        episode.Season.SeasonNumber >= 1) +
                    tvShow.Seasons
                        .Where(season => season.SeasonNumber >= 1 && season.Episodes.Count == 0)
                        .Sum(season => season.EpisodeCount ?? 0),
                RegularWatchedEpisodes = dbContext.WatchedEpisodes.Count(watchedEpisode =>
                    watchedEpisode.UserId == userId &&
                    watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                    watchedEpisode.Episode.Season.SeasonNumber >= 1),
                LastWatchedAt = dbContext.WatchedEpisodes
                    .Where(watchedEpisode =>
                        watchedEpisode.UserId == userId &&
                        watchedEpisode.Episode.Season.TvShowId == tvShow.Id &&
                        watchedEpisode.Episode.Season.SeasonNumber >= 1)
                    .Max(watchedEpisode => (DateTime?)watchedEpisode.WatchedAt),
            });
    }
}

internal sealed class TvShowCompletionRow
{
    public Guid TvShowId { get; init; }

    public bool IsConcluded { get; init; }

    public int RegularTotalEpisodes { get; init; }

    public int RegularWatchedEpisodes { get; init; }

    public DateTime? LastWatchedAt { get; init; }
}
