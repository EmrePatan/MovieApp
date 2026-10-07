using System.Linq.Expressions;
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
    internal static readonly Expression<Func<TvShowCompletionRow, bool>> IsCompleted = row =>
        row.IsConcluded &&
        row.RegularTotalEpisodes > 0 &&
        row.RegularWatchedEpisodes >= row.RegularTotalEpisodes;

    internal static readonly Expression<Func<TvShowCompletionRow, bool>> IsInProgress = row =>
        !(row.IsConcluded &&
          row.RegularTotalEpisodes > 0 &&
          row.RegularWatchedEpisodes >= row.RegularTotalEpisodes);

    /// <summary>
    /// One row per show in which the user has watched at least one regular (season ≥ 1) episode.
    /// Relational providers use one grouped statement. The in-memory provider keeps the original
    /// correlated shape so unit tests that execute LINQ in memory stay equivalent.
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
        return dbContext.Database.SqlQuery<TvShowCompletionRow>($"""
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
                WHERE watched_episode."UserId" = {userId}
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
                      WHERE started_watch."UserId" = {userId}
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
                      WHERE started_watch."UserId" = {userId}
                        AND started_season."SeasonNumber" >= 1
                  )
                GROUP BY season."TvShowId"
            ) AS empty_seasons ON empty_seasons.tv_show_id = watched.tv_show_id
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
